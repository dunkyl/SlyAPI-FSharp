namespace net.dunkyl.SlyAPI

open System
open System.Net.Http
open System.Text.Json
open System.Threading.Tasks

/// Shorthand for a async api call
type Call<'T> = Task<Result<'T, Net.HttpStatusCode * HttpResponseMessage>>

[<AutoOpen>]
module Extensions =

    open System.Runtime.CompilerServices

    /// Not raised by any internal source, but used to convert to exception-style
    /// error handling from `FSharp.Core.Result` with the `Unwrap()` extension method.
    exception APIException of Net.HttpStatusCode * HttpResponseMessage

    /// Returns the Ok value, or raises the error value as an `APIException`.
    [<Extension>]
    type ResultUnwrapExtension () =
        [<Extension>]
        static member inline Unwrap(this: Result<'T, Net.HttpStatusCode * HttpResponseMessage>) =
            match this with
            | Ok x -> x
            | Error e -> raise (APIException e)

/// Base class for API implentations.
/// Handles authorization, serialization, and requests.
type [<AbstractClass>] WebAPI (auth: Auth) =
    
    let client = new HttpClient()
    
    abstract member BaseURL: Uri
    abstract member UserAgent: string
    default _.UserAgent = $"SlyAPI-FSharp/0.0.1"
    
    /// Options used for all serialization and deserialization of API calls
    member _.JsonOptions =
        let opts = JsonSerializerOptions(
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            NumberHandling = Serialization.JsonNumberHandling.AllowReadingFromString
            )
        opts.Converters.Add(Serialization.EnumUnionJsonConverter())
        opts

    /// Add authorization to a request and send it
    member this.RawRequest (request: HttpRequestMessage): Call<HttpResponseMessage> = task {
        request.Headers.Add("User-Agent", this.UserAgent)
        let! signedRequest = auth.Sign(request, client)
        let! result = client.SendAsync(signedRequest)
        if result.IsSuccessStatusCode then
            return Ok(result)
        else
            return Error(result.StatusCode, result)
    }
    
    /// Serialized call, deserialize response
    /// Output type should be `unit` if the expected response code is 204
    /// Input type should be `unit` if only path or query args are needed
    member this.Call method (path: string) (input: 'In): 'Out Call =
        let req =
            match input :> obj with
            | :? unit -> new HttpRequestMessage(method, Uri(this.BaseURL, path))
            | _ ->
                let content = Json.JsonContent.Create<_>(input, options = this.JsonOptions)
                new HttpRequestMessage(method, Uri(this.BaseURL, path), Content = content)
        task {
            match! this.RawRequest req with
            | Ok data ->
                match data.StatusCode with
                | Net.HttpStatusCode.OK ->
                    let! json = data.Content.ReadAsStringAsync()
                    return Ok (JsonSerializer.Deserialize<_>(json, this.JsonOptions))
                | Net.HttpStatusCode.NoContent ->
                    return Ok Unchecked.defaultof<'Out>
                | other ->
                    Diagnostics.Debug.WriteLine($"Unexpected success error code: {other}")
                    return Error (other, data)
            | Error err ->
                return Error err
        }
    
    /// Serialized GET, deserialize response
    member this.Get path (input: 'In): 'Out Call = this.Call HttpMethod.Get path input