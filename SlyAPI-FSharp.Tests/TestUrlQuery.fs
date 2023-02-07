namespace SlyAPI_FSharp.Tests

open System
open Microsoft.VisualStudio.TestTools.UnitTesting

open System
open net.dunkyl.SlyAPI

type DU = FirstCase | SecondCase

[<TestClass>]
type TestUrlQuery () =

    [<TestMethod>]
    member _.TestNoParams () =
        
        let q = urlQuery "test" [ ]
    
        Assert.AreEqual ("test", q)

    [<TestMethod>]
    member _.Test1Param () =
        
        let q = urlQuery "test" [ "a", 1 ]
    
        Assert.AreEqual ("test?a=1", q)

    [<TestMethod>]
    member _.Test2Params () =
        
        let q = urlQuery "test" [ "a", 1; "bee", "hi 😀" ]
    
        Assert.AreEqual ("test?a=1&bee=hi+%f0%9f%98%80", q)

    [<TestMethod>]
    member _.TestDiscrimParam () =
        
        let q = urlQuery "test" [ "a", FirstCase ]
    
        Assert.AreEqual ("test?a=FirstCase", q)

    [<TestMethod>]
    member _.TestManyParams () =
        
        let q = urlQuery "test" [
            "a", 1
            "bee", "hi 😀"
            "bool", false
            "byte", 6y
            "unsigned byte", 240uy
            "short", 16s
            "ushort", 16us
            "int", 16
            "uint", 16u
            "long", 16L
            "ulong", 16UL
            "float", 3.14f
            "double", 3.14
            "decimal", 3.14m
        ]
        
        let expected =
            [   "bool", "False"
                "byte", "6"
                "unsigned+byte", "240"
                "short", "16"
                "ushort", "16"
                "int", "16"
                "uint", "16"
                "long", "16"
                "ulong", "16"
                "float", "3.14"
                "double", "3.14"
                "decimal", "3.14"
            ] |> Seq.map (fun (k, v) -> $"{k}={v}")
              |> String.concat "&"
    
        Assert.AreEqual ("test?a=1&bee=hi+%f0%9f%98%80&" + expected, q)
