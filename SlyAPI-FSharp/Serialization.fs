module net.dunkyl.SlyAPI.Serialization

open FSharp.Reflection
open System.Text.Json
open System.Text.Json.Serialization

let isEnumUnion value =
    let ty = value.GetType()
    FSharpType.IsUnion ty
    && (
        let cases = FSharpType.GetUnionCases(ty)
        not (Array.exists (fun (c: UnionCaseInfo) -> c.GetFields().Length <> 0) cases)
    )

let getUnionName value =
    let (case, _) = FSharpValue.GetUnionFields(value, value.GetType())
    case.Name
    
/// Serializes F# Discriminated Unions, but only in the case that 
/// none of the cases have any arguements. This is intended to act 
/// very similiar to C# Enums and the `JsonStringEnumConverter`
type EnumUnionJsonConverter () =
    inherit JsonConverter<obj>() with 
    
        override _.CanConvert value = isEnumUnion value

        override _.Read(reader, ty, options) =
            let mutable name = reader.GetString ()
            let convertName =
                options.PropertyNamingPolicy.ConvertName
                >>
                if options.PropertyNameCaseInsensitive then
                    name <- name.ToLower()
                    fun name -> name.ToLower()
                else
                    id
            let search (c: UnionCaseInfo) = convertName(c.Name) = name
            let cases = FSharpType.GetUnionCases ty
            FSharpValue.MakeUnion(Array.find search cases, [||])
            
        override _.Write(writer, value, options) =
            value
            |> getUnionName
            |> options.PropertyNamingPolicy.ConvertName
            |> writer.WriteStringValue

let (|EnumUnion|_|) (u: obj) =
    if isEnumUnion u then
        Some (getUnionName u)
    else
        None