namespace net.dunkyl.SlyAPI

open System
open System.Security.Cryptography
open System.Text
open System.Net.Http
open System.Text.Json
open System.Threading.Tasks
open System.IO

open Tokens

/// Authorized user grant
type OAuth2User = {
    Token: string
    RefreshToken: string
    Expires: DateTime
    TokenType: string
    Scopes: string Set
}

/// Project credentials for creating user tokens
type OAuth2App = {
    Id: string
    Secret: string
    AuthUri: string
    TokenUri: string
} with
    
    member this.AuthUrlWithPkce(redirectUrl: Uri, state: string, scopes: string Set) =
        let stateChallenge = tokenUrlSafe 54
        let codeVerifier = tokenUrlSafe 54
        let verifierHash = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(codeVerifier))
        let codeChallenge = urlSafeEncode64 verifierHash
        let params' = [|
            "client_id",             this.Id
            "redirect_uri",          redirectUrl.ToString()
            "response_type",         "code"
            "scope",                 String.Join(' ', scopes)
            "state",                 stateChallenge + ":" + state
            "code_challenge",        codeChallenge
            "code_challenge_method", "S256"
        |]
        let queries =
            Array.map (fun (k: string, v) ->
               k + "=" + Uri.EscapeDataString(v)
            ) params'
        let query = String.Join('&', queries)
        Uri $"{this.AuthUri}?{query}", codeVerifier, stateChallenge


    member this.ExchangeCodeWithPkce (client: HttpClient) code codeVerifier (scopes: string Set) (redirect: Uri) = task {
        // TODO: implement other flavours of exchange such as for twitter
        let content = new FormUrlEncodedContent(Map.ofArray [|
            "grant_type",    "authorization_code"
            "code",          code
            "scope",         String.Join(' ', scopes) // TODO: do i really need scopes here, even for twitter?
            "code_verifier", codeVerifier
            "client_id",     this.Id
            "client_secret", this.Secret
            "redirect_uri",  redirect.ToString()
        |])
        let request = new HttpRequestMessage(HttpMethod.Post, this.TokenUri, Content = content)
        request.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(this.Id + ":" + this.Secret)))
        let! response = client.SendAsync(request)
        let! text = response.Content.ReadAsStringAsync()
        let grant = text |> JsonSerializer.Deserialize<{|
            access_token: string
            refresh_token: string
            expires_in: int
            scope: string
            token_type: string |}>
        let user = {
            Token = grant.access_token
            RefreshToken = grant.refresh_token
            Expires = DateTime.Now.AddSeconds(float grant.expires_in)
            TokenType = grant.token_type
            Scopes = grant.scope.Split(' ') |> Set.ofArray
        }
        return user
    }

    member this.Refresh(client: HttpClient) (user: OAuth2User) = task {
        let content = new FormUrlEncodedContent(Map.ofArray [|
            "grant_type", "refresh_token"
            "refresh_token", user.RefreshToken
            "client_id", this.Id
            "client_secret", this.Secret
        |])
        return! client.PostAsync(this.TokenUri, content)
        |> Call.mapResponse (fun r -> task {
            let! text = r.Content.ReadAsStringAsync()
            let tokenResponse = JsonSerializer.Deserialize<
                {| access_token: string; expires_in: int |}> text
            let newExpiry = DateTime.Now + TimeSpan(0, 0, tokenResponse.expires_in)
            return { user with Token = tokenResponse.access_token; Expires = newExpiry}
        })
    }

    member this.WithUser (user: OAuth2User) = OAuth2 (this, user)

/// App with authorized user.
/// Automatically refreshes the token if it expires
and OAuth2 (app: OAuth2App, user: OAuth2User, ?onRefresh: Action<OAuth2User>) =

    let is_refreshing = new Threading.SemaphoreSlim(0, 1)
    
    interface IOAuth2 with
        member this.Sign(request: HttpRequestMessage, client: HttpClient) = task {
            do! is_refreshing.WaitAsync()
            let! refresh = 
                if this.User.Expires < DateTime.Now then
                    this.Refresh(client)
                else
                    Task.FromResult (Ok ())
            is_refreshing.Release() |> ignore
            match refresh with
            | Ok () ->
                request.Headers.Add("Authorization", $"{this.User.TokenType} {this.User.Token}")
                return Ok request
            | Error e -> return Error e
        }
        
    /// Load the app and user from JSON files
    /// When the user is refreshed, it will be rewritten to the user file
    new(appFile: string, userFile: string) =
        let jsonOptions = JsonSerializerOptions(
            PropertyNamingPolicy = Serialization.SnakeCaseNamingPolicy ()
        )
        let app = JsonSerializer.Deserialize<OAuth2App>(
            File.ReadAllText appFile,
            jsonOptions)
        let user = JsonSerializer.Deserialize<OAuth2User>(
            File.ReadAllText userFile,
            jsonOptions)
        let writeUser user =
            let json = JsonSerializer.Serialize<OAuth2User>(user, jsonOptions)
            File.WriteAllText(userFile, json)
            
        OAuth2 (app, user, writeUser)
        
    member val User = user with get, set
    member val App = app with get

    member private this.Refresh(client: HttpClient): unit Call = task {
        match! app.Refresh client user with
        | Ok newUser ->
            this.User <- newUser
            match onRefresh with
            | Some(fn) -> fn.Invoke(this.User)
            | None -> ()
            return Ok ()
        | Error e -> return Error e
    }

type private PendingGrant = {
    CodeVerifier: string
    StateChallenge: string
    Scopes: string Set
    Redirect: Uri
    BeganAt: DateTime
}

/// Mangages the steps for granting user tokens for an app
type PkceOAuth2Wizard (app: OAuth2App, ?client: HttpClient) =

    let client = Option.defaultWith (fun () -> new HttpClient()) client

    member val Timeout = TimeSpan.FromMinutes(5.) with get, set
    member val private PendingAuthentications = System.Collections.Concurrent.ConcurrentDictionary<_, _>() with get, set
    
    member private this.IsExpiredBy (time) (grant) =
        time - grant.BeganAt > this.Timeout

    member private this.PushUnverified (stateChallenge: string, codeVerifier: string, scopes: string Set, redirect: Uri) =
        this.PruneGrants()
        let newPending = {
            CodeVerifier = codeVerifier
            StateChallenge = stateChallenge
            BeganAt = DateTime.Now
            Scopes = scopes
            Redirect = redirect
        }
        this.PendingAuthentications.TryAdd(stateChallenge, newPending)
        |> ignore
        
    member private this.PruneGrants() =
        let now = DateTime.Now
        for kv in this.PendingAuthentications do
            if this.IsExpiredBy now kv.Value then
                let (_present, _grant) = this.PendingAuthentications.TryRemove(kv.Key)
                ()
        
    member private this.PopVerified (state: string) =
        let now = DateTime.Now
        let proposedStateChallenge = state.Split(':', 2)[0]
        let (present, grant) = this.PendingAuthentications.TryRemove(proposedStateChallenge)
        if present then
            if this.IsExpiredBy now grant then
                failwith "Grant expired"
            else
                grant
        else 
            failwith "Grant not found or invalid"
    
    /// Generate a URL on a third party website.
    /// User is redirect to this by 1st party
    member this.Step1 state redirect scopes =
        let authUrl, codeVerifier, stateChallenge =
            app.AuthUrlWithPkce(redirect, state, scopes)
        this.PushUnverified(stateChallenge, codeVerifier, scopes, redirect)
        authUrl

    /// Exchange a code from a user who was redirected back for a grant 
    member this.Step3 state code =
        let grant = this.PopVerified(state)
        app.ExchangeCodeWithPkce client code grant.CodeVerifier grant.Scopes grant.Redirect
            
            