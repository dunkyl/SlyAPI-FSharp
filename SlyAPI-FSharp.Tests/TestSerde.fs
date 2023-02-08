namespace SlyAPI_FSharp.Tests

open System
open Microsoft.VisualStudio.TestTools.UnitTesting

open System
open net.dunkyl.SlyAPI

[<TestClass>]
type TestSerde () =

    [<TestMethod>]
    member _.TestOAuth2FromFiles () =
        let auth = OAuth2("test app.json", "test user.json")

        ()