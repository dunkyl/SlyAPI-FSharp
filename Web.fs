namespace net.dunkyl.SlyAPI

open System
open System.Net.Http
open System.Threading.Tasks


/// Shorthand for a async api call
type Call<'T> = Task<Result<'T, HttpResponseMessage>>

module Call =
    let map (fn: 'T -> 'U) (call: Call<'T>): Call<'U> = task {
        let! result = call
        return result |> Result.map fn
    }

    let mapTask (fn: 'T -> Task<'U>) (call: Call<'T>): Call<'U> = task {
        let! result = call
        match result with
        | Ok x ->
            let! result2 = fn x
            return Ok result2
        | Error err ->
            return Error err
    }

    let mapResponse (fn: HttpResponseMessage -> Task<'U>) (r: Task<HttpResponseMessage>) = task {
        let! r = r
        if r.IsSuccessStatusCode then
            let! inner = fn r
            return Ok inner
        else
            return Error r
    }
        