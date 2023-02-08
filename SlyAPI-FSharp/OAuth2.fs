namespace net.dunkyl.SlyAPI

open System
open System.Security.Cryptography
open System.Text
open System.Net.Http
open System.Text.Json
open System.Threading.Tasks
open System.IO

/// Generating and processing tokens for OAuth2
module Tokens =

    let private rng = RandomNumberGenerator.Create()

    /// Base-64 encoding with the url safe digit set
    /// https://www.rfc-editor.org/rfc/rfc4648#section-5
    let urlSafeEncode64 bytes =
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')

    /// Make a new string token of the length
    /// For OAuth2 PKCE, the number of bytes is at least 32 (43 digits)
    /// and at most 96 (128 digits)
    /// https://www.rfc-editor.org/rfc/rfc7636#section-4.1
    let tokenUrlSafe length =
        let bytes = Array.zeroCreate<byte> length
        rng.GetBytes(bytes)
        urlSafeEncode64 bytes
    
    /// Url-safe-base-64-encoded hash of a token
    let sha256ofToken (token: string) =
        token
        |> Encoding.UTF8.GetBytes
        |> SHA256.Create().ComputeHash
        |> urlSafeEncode64

/// Authorized user grant
type OAuth2User = {
    Token: string
    RefreshToken: string
    Expires: DateTime
    TokenType: string
    Scopes: string Set
}

open Tokens

/// Project credentials for creating user tokens
type OAuth2App = {
    Id: string
    Secret: string
    AuthUrl: string
    TokenUrl: string
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
        Uri $"{this.AuthUrl}?{query}", codeVerifier, stateChallenge


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
        let request = new HttpRequestMessage(HttpMethod.Post, this.TokenUrl, Content = content)
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
        let! response = client.PostAsync(this.TokenUrl, content)
        let! text = response.Content.ReadAsStringAsync()
        if response.IsSuccessStatusCode then
            let tokenResponse = JsonSerializer.Deserialize<
                {| access_token: string; expires_in: int |}> text
            let newExpiry = DateTime.Now + TimeSpan(0, 0, tokenResponse.expires_in)
            return { user with Token = tokenResponse.access_token; Expires = newExpiry}
        else
            Diagnostics.Debug.WriteLine $"{text}"
            return failwith $"Failed to refresh token: {response.StatusCode}"
    }

    member this.WithUser (user: OAuth2User) = OAuth2 (this, user)

/// App with authorized user.
/// Automatically refreshes the token if it expires
and OAuth2 (app: OAuth2App, user: OAuth2User, ?userRefreshCallback: Action<OAuth2User>) =

    let mutable refreshTask: Task option = None
    
    interface Auth with
        member this.Sign(request: HttpRequestMessage, client: HttpClient) = task {
            if user.Expires < DateTime.Now then
                match refreshTask with
                | Some(task) ->
                    do! task // TODO: check that this doesn't run it twice
                | None ->
                    let task = this.Refresh(client)
                    refreshTask <- Some(task)
                    do! task
                refreshTask <- None
            request.Headers.Add("Authorization", $"{user.TokenType} {user.Token}")
            return request
        }
        
    new(appFile: string, userFile: string) =
        let app = 
            appFile
            |> File.ReadAllText
            |> JsonSerializer.Deserialize<OAuth2App>
        let user = 
            userFile
            |> File.ReadAllText
            |> JsonSerializer.Deserialize<OAuth2User>
        OAuth2 (app, user)
        
    member val User = user with get, set

    member private this.Refresh(client: HttpClient) = task {
        let! newUser = app.Refresh client user
        this.User <- newUser
        match userRefreshCallback with
        | Some(fn) -> fn.Invoke(newUser)
        | None -> ()
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
            
            