open System.Text.Json
open System.Net.Http
open System
open System.Collections.Generic
open net.dunkyl.SlyAPI.Jwt

open net.dunkyl.SlyAPI.ServiceAccounts

let args =
    System.Environment.GetCommandLineArgs()
    |> List.ofArray
    |> List.skip 1

args
|> String.concat ", "
|> printfn "Args: [%s]"

let grant scope service'account =
    let jwt = googleJsonToJwt scope service'account.PrivateKey service'account.ClientEmail
    let http = new HttpClient()
    use req = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token")
    req.Content <- new FormUrlEncodedContent([
        KeyValuePair("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer")
        KeyValuePair("assertion", jwt)
    ])
    let text = Async.RunSynchronously <| async {
        let! r = http.SendAsync req |> Async.AwaitTask
        let! c = r.Content.ReadAsStringAsync() |> Async.AwaitTask
        return c
    }
    printfn "Response: %s" text
    let json = JsonDocument.Parse(text)
    let iserror, errorname = json.RootElement.TryGetProperty("error")
    if iserror then
        let errorname = errorname.GetString()
        let error_description = json.RootElement.GetProperty("error_description").GetString()
        failwithf "Error: %s: %s" errorname error_description
    let token = json.RootElement.GetProperty("access_token").GetString()
    let expires_in = json.RootElement.GetProperty("expires_in").GetInt64()
    let expiry = DateTime.Now + TimeSpan.FromSeconds(float expires_in)
    let granted_scope =
        let present, el =json.RootElement.TryGetProperty("scope")
        if present then el.GetString() else ""
    let token_type = json.RootElement.GetProperty("token_type").GetString()
    let grant = {|
        Token = token
        TokenType = token_type
        Expiry = expiry
        Scopes = granted_scope.Split(' ', StringSplitOptions.RemoveEmptyEntries)
    |}
    printf "%A" grant

match args with
| "service-grant"::jsonFile::scopes ->
    let json = System.IO.File.ReadAllText(jsonFile)
    let jsonOptions = JsonSerializerOptions(
        PropertyNamingPolicy = net.dunkyl.SlyAPI.Serialization.SnakeCaseNamingPolicy ()
    )
    let sa: ServiceAccount = JsonSerializer.Deserialize<_>(json, jsonOptions)
    grant (String.concat " " scopes) sa
| _ ->
    printfn "Usage: service-grant <google-service-account-json> scopes..."