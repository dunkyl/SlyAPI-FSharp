namespace net.dunkyl.SlyAPI

open System
open System.Net.Http
open System.Threading.Tasks

/// Authentication scheme for accessing a particular API
type IAuth =
    /// Add necessary headers or options to authorize a request
    abstract member Sign: HttpRequestMessage * HttpClient -> Call<HttpRequestMessage>

/// Symbolizes that the API accepts some kind of OAuth2
type IOAuth2 = inherit IAuth

/// Key passed as a key in an HTTP header
type HeaderAPIKey (paramName: string, key: string) =
    interface IAuth with
        member _.Sign (req, _) =
            req.Headers.Add(paramName, key)
            Task.FromResult (Ok req)

/// Key passed as a URL query paramter
type QueryAPIKey (paramName: string, key: string) =
    interface IAuth with
        member _.Sign (req, _) =
            let q = Web.HttpUtility.ParseQueryString req.RequestUri.Query
            q.Add(paramName, key)
            req.RequestUri <- Uri(req.RequestUri.GetLeftPart(UriPartial.Path) + "?" + q.ToString())
            Task.FromResult (Ok req)

/// No authentication
type NoAuth () =
    interface IAuth with
        member _.Sign (req, _) = Task.FromResult (Ok req)