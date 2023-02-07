namespace net.dunkyl.SlyAPI

open System
open System.Net.Http
open System.Threading.Tasks

/// Authentication scheme for accessing a particular API
type Auth =
    /// Add necessary headers or options to authorize a request
    abstract member Sign: HttpRequestMessage * HttpClient -> Task<HttpRequestMessage>

/// Key passed as a key in an HTTP header
type HeaderAPIKey (paramName: string, key: string) =
    interface Auth with
        member _.Sign (req, _) =
            req.Headers.Add(paramName, key)
            Task.FromResult req

/// Key passed as a URL query paramter
type QueryAPIKey (paramName: string, key: string) =
    interface Auth with
        member _.Sign (req, _) =
            let q = Web.HttpUtility.ParseQueryString req.RequestUri.Query
            q.Add(paramName, key)
            req.RequestUri <- Uri(req.RequestUri.GetLeftPart(UriPartial.Path) + "?" + q.ToString())
            Task.FromResult req

/// No authentication
type NoAuth () =
    interface Auth with
        member _.Sign (req, _) = Task.FromResult req