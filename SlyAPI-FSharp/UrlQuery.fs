namespace net.dunkyl.SlyAPI

open System

open System.Collections.Generic
open Serialization

[<AutoOpen>]
module UrlQuery =
    /// Serialize key-value pairs into a URL query string
    let urlQuery (path: string) (qparams: (string * obj) IReadOnlyCollection) =
        if qparams.Count = 0 then
            path
        else
            path + "?" +
            String.concat "&" [
                for k, v in qparams ->
                    match v with
                    | EnumUnion name -> name
                    | :? IConvertible as from -> from.ToString()
                    | _ -> failwith "unimplemented"
                    |> Web.HttpUtility.UrlEncode
                    |> sprintf "%s=%s" (Web.HttpUtility.UrlEncode k)
            ]