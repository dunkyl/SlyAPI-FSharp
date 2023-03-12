/// Non-user OAuth2 grants for server-to-server communication
module net.dunkyl.SlyAPI.ServiceAccounts

open System.Net.Http
open System.Text.Json
open System.IO
open System
open System.Collections.Generic
open System.Threading.Tasks

open Jwt

type ServiceGrant = {
    AccessToken: string
    Expiry: DateTime
    TokenType: string
    Scopes: string Set
}

type ServiceAccount = {
    ClientEmail: string
    ClientId: string
    PrivateKey: string
    AuthUri: string
    TokenUri: string
} with
    member this.Token (client: HttpClient) (scopes: string IEnumerable) = task {
        let jwt = googleJsonToJwt (String.concat " " scopes) this.PrivateKey this.ClientEmail
        use req = new HttpRequestMessage(HttpMethod.Post, this.TokenUri)
        req.Content <- new FormUrlEncodedContent([
            KeyValuePair("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer")
            KeyValuePair("assertion", jwt)
        ])
        return!
            client.SendAsync req
            |> Call.mapResponse (fun r -> task { return! r.Content.ReadAsStringAsync() })
            |> Call.map (fun text ->
                let json = (JsonDocument.Parse text).RootElement
                let token = json.GetProperty("access_token").GetString()
                let expires_in = json.GetProperty("expires_in").GetInt64()
                let expiry = DateTime.Now + TimeSpan.FromSeconds(float expires_in)
                let token_type = json.GetProperty("token_type").GetString()
                {
                    AccessToken = token
                    TokenType = token_type
                    Expiry = expiry
                    Scopes = Set.ofSeq scopes
                }) 
    }

let ServiceAccount (path: string): ServiceAccount =
    let jsonOptions = JsonSerializerOptions(
        PropertyNamingPolicy = Serialization.SnakeCaseNamingPolicy ()
    )
    let json = File.ReadAllText path
    JsonSerializer.Deserialize<_>(json, jsonOptions)

let ServiceAccountGrant (path: string): ServiceGrant =
    let jsonOptions = JsonSerializerOptions(
        PropertyNamingPolicy = Serialization.SnakeCaseNamingPolicy ()
    )
    let json = File.ReadAllText path
    JsonSerializer.Deserialize<_>(json, jsonOptions)

type OAuth2ServiceAccount(account: ServiceAccount, grant: ServiceGrant, ?onRefresh: ServiceGrant -> unit) = 
    //let mutable refreshTask: Task option = None
    let is_refreshing = new System.Threading.SemaphoreSlim(1, 1)

    member val Grant = grant with get, set
    member val Account = account with get

    interface IOAuth2 with
       member this.Sign(request: HttpRequestMessage, client: HttpClient) = task {
           do! is_refreshing.WaitAsync()
           let! refresh = 
               if this.Grant.Expiry < DateTime.Now then
                   this.Refresh(client)
               else
                   Task.FromResult (Ok ())
           is_refreshing.Release() |> ignore
           match refresh with
           | Ok () ->
               request.Headers.Add("Authorization", $"{this.Grant.TokenType} {this.Grant.AccessToken}")
               return Ok request
           | Error e -> return Error e
       }

    member this.Refresh(client: HttpClient) = task {
        let! new'grant = this.Account.Token client this.Grant.Scopes
        match new'grant with
        | Ok grant ->
            this.Grant <- grant
            match onRefresh with
            | Some fn -> fn this.Grant
            | None -> ()
            return Ok ()
        | Error e -> return Error e
    }

    static member Create(
            account: ServiceAccount, scopes: string IEnumerable,
            ?onRefresh: ServiceGrant -> unit, ?client: HttpClient) = task {
        let client = Option.defaultWith (fun () -> new HttpClient()) client 
        let! grant = account.Token client scopes
        return OAuth2ServiceAccount(account, grant.Unwrap(), ?onRefresh=onRefresh)
    }
        