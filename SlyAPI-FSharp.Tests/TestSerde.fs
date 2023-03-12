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
        printfn $"{auth.App}"
        printfn $"{auth.User}"

        Assert.IsNotNull auth.App.Id
        Assert.IsNotNull auth.App.Secret
        Assert.IsNotNull auth.App.AuthUri
        Assert.IsNotNull auth.App.TokenUri
        
        Assert.IsNotNull auth.User.Expires
        Assert.IsTrue (auth.User.Expires > DateTime(2000, 1, 1))
        Assert.IsNotNull auth.User.RefreshToken
        Assert.IsNotNull auth.User.Scopes
        Assert.IsNotNull auth.User.Token
        Assert.IsNotNull auth.User.TokenType

    [<TestMethod>]
    member _.TestServiceAccountFromFile () =
        let sa = ServiceAccounts.ServiceAccount("test service account.json")

        Assert.IsNotNull sa.AuthUri
        Assert.IsNotNull sa.ClientEmail
        Assert.IsNotNull sa.PrivateKey
        Assert.IsNotNull sa.AuthUri
        Assert.IsNotNull sa.TokenUri
