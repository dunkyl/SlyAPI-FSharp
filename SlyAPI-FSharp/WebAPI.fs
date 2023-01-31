namespace net.dunkyl.SlyAPI

open System
open System.Net.Http
open System.Text.Json
open System.Threading.Tasks

module private Meta =
    // TODO decide how to control version numbers
    let VERSION = "0.0.1"
    
type APIResult<'Success> = Result<'Success, Net.HttpStatusCode * HttpResponseMessage>

/// Shorthand for a async api call
type Call<'T> = Task<APIResult<'T>>

/// A set of string values that are serialized together as a delimited list under one key
/// Common parameter type
type EnumParams = {
    Title: string
    Values: string Set
} with
    member this.Contains (other: EnumParams) =
        Set.isSuperset this.Values other.Values
            &&
        this.Title = other.Title
    
    static member (+) (lhs, rhs) =
        if lhs.Title <> rhs.Title then
            failwith "Cannot add two EnumParams with different titles"
        else
            { Title = lhs.Title
              Values = Set.union lhs.Values rhs.Values }

    static member Combine (params': EnumParams list): Map<string, string Set> =
        Seq.fold (fun obj param ->
            obj.Add (param.Title, (
                match Map.tryFind param.Title obj with
                | None -> param.Values
                | Some(values) -> Set.union values param.Values
            ))
        ) Map.empty params'

type [<AbstractClass>] WebAPI (auth: Auth) =
    
    let client = new HttpClient()

    abstract member BaseURL: Uri
    abstract member UserAgent: string
    default _.UserAgent = $"SlyAPI-FSharp/{Meta.VERSION}"

    member _.JsonOpts = JsonSerializerOptions(
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = Serialization.JsonNumberHandling.AllowReadingFromString
        )

    member private this.SendRequest(request: HttpRequestMessage): Call<HttpResponseMessage> = task {
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
    member this.Call<'In, 'Out> method (path: string) (input: 'In): Call<'Out> =
        let req =
            match input :> obj with
            | :? HttpRequestMessage as req -> req
            | :? unit -> new HttpRequestMessage(method, Uri(this.BaseURL, path))
            | _ ->
                let content = Json.JsonContent.Create<_>(input, options = this.JsonOpts)
                new HttpRequestMessage(method, Uri(this.BaseURL, path), Content = content)
        task {
            match! this.SendRequest req with
            | Ok data ->
                match data.StatusCode with
                | Net.HttpStatusCode.OK ->
                    let! json = data.Content.ReadAsStringAsync()
                    return Ok (JsonSerializer.Deserialize<_>(json, this.JsonOpts))
                | Net.HttpStatusCode.NoContent ->
                    return Ok Unchecked.defaultof<'Out>
                | other ->
                    Diagnostics.Debug.WriteLine($"Unexpected success error code: {other}")
                    return Error (other, data)
            | Error err ->
                return Error err
        }
    
    /// Serialized GET, deserialize response
    member this.Get<'In, 'Out> path input =
        this.Call<'In, 'Out> Net.Http.HttpMethod.Get path input