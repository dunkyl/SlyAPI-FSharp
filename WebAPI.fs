namespace net.dunkyl.SlyAPI

open System
open System.Net.Http
open System.Text.Json

[<AutoOpen>]
module Extensions =

    open System.Runtime.CompilerServices

    /// Not raised by any internal source, but used to convert to exception-style
    /// error handling from `FSharp.Core.Result` with the `Unwrap()` extension method.
    exception APIException of HttpResponseMessage

    /// Returns the Ok value, or raises the error value as an `APIException`.
    [<Extension>]
    type ResultUnwrapExtension () =
        [<Extension>]
        static member inline Unwrap(this: Result<'T, HttpResponseMessage>) =
            match this with
            | Ok x -> x
            | Error e -> raise (APIException e)

/// Base class for API implentations.
/// Handles authorization, serialization, and requests.
type [<AbstractClass>] WebAPI (auth: IAuth) =
    
    let client = new HttpClient()
    
    abstract member BaseURL: Uri
    abstract member UserAgent: string
    default _.UserAgent = $"SlyAPI-FSharp/0.0.6"
    
    /// Options used for all serialization and deserialization of API calls
    member _.JsonOptions =
        let opts = JsonSerializerOptions(
            PropertyNamingPolicy = Serialization.SnakeCaseNamingPolicy(),
            NumberHandling = Serialization.JsonNumberHandling.AllowReadingFromString
            )
        opts.Converters.Add(Serialization.EnumUnionJsonConverter())
        opts

    /// Add authorization to a request and send it
    member this.RawRequest (request: HttpRequestMessage): Call<HttpResponseMessage> = task {
        request.Headers.Add("User-Agent", this.UserAgent)
        let! signedRequest = auth.Sign(request, client)
        match signedRequest with
        | Ok req ->
            let! result = client.SendAsync req
            if result.IsSuccessStatusCode then
                return Ok result
            else
                return Error result
        | Error e -> return Error e
        // ^ note: result type differs from inner result (success as request/response)
    }

    /// Serialized call with no deserializtion
    member this.SerializedCall method (path: string) (input: 'In): HttpResponseMessage Call = 
        let uri = Uri(this.BaseURL, path)
        let req =
            match input :> obj with
            | :? unit -> new HttpRequestMessage(method, uri)
            | _ ->
                let content = Json.JsonContent.Create<_>(input, options = this.JsonOptions)
                new HttpRequestMessage(method, uri, Content = content)
        this.RawRequest req
    
    /// Serialized call, deserialize response
    /// Output type should be `unit` if the expected response code is 204
    /// Input type should be `unit` if only path or query args are needed
    member this.Call method (path: string) (input: 'In): 'Out Call = task {
        let! response = this.SerializedCall method path input
        match response with
        | Ok data ->
            match data.StatusCode with
            | Net.HttpStatusCode.OK ->
                let! json = data.Content.ReadAsStringAsync()
                return Ok (JsonSerializer.Deserialize<_>(json, this.JsonOptions))
            | Net.HttpStatusCode.NoContent when typeof<'Out> = typeof<unit> ->
                return Ok Unchecked.defaultof<'Out>
            | other ->
                Diagnostics.Debug.WriteLine($"Unexpected success error code: {other}")
                return Error data
        | Error err ->
            return Error err
    }
    
    /// Serialized GET, deserialize response
    member this.Get path (input: 'In): 'Out Call = this.Call HttpMethod.Get path input
    
    /// Serialized GET, text response
    member this.GetString path (input: 'In): string Call =
        Call.mapTask
            (fun (res: HttpResponseMessage) -> res.Content.ReadAsStringAsync())
            (this.SerializedCall HttpMethod.Get path input)