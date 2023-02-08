namespace net.dunkyl.SlyAPI

open System

[<AttributeUsage(System.AttributeTargets.Method)>]
type RequiresScopesAttribute([<ParamArray>] scopes: string array) =
    inherit Attribute()
    
    member val Scopes = scopes with get, set