namespace SlyAPI_FSharp.Tests

open System
open Microsoft.VisualStudio.TestTools.UnitTesting

open System
open net.dunkyl.SlyAPI

open System.IO
open System.Threading.Tasks

type Units = Standard (* Kelvin *) | Imperial | Metric

type CityWeather = {
    Name: string
    Main: {| Temp: float |}
    Weather: {| Description: string |} list
}

type OpenWeather (key: string) =
    inherit WebAPI(QueryAPIKey("appid", key))
    
    override _.BaseURL = Uri "https://api.openweathermap.org/data/2.5/"
    override _.UserAgent = "YourWeatherAppLibrary/0.99.0"

    /// Get the current weather of a city.
    /// Location format: `City,Country` or `City,State,Country`
    /// where State and Country are ISO3166 codes.
    member this.City(location: string, units: Units): CityWeather Call =
        this.Get (urlQuery "weather" [ "units", units; "q", location ]) ()

[<TestClass>]
type TestReadme () =

    [<TestMethod>]
    member _.TestAPICall () =
        let weather = "apikey.txt" |> File.ReadAllText |> OpenWeather

        let current = weather.City ("London,uk", Standard)

        Task.WaitAll current
        
        match current.Result with
        | Ok city ->
            printfn $"{city}"
            Assert.AreEqual ("London", city.Name)
        | Error (code, message) ->
            let text' = message.Content.ReadAsStringAsync()
            Task.WaitAll text'
            Assert.Fail(sprintf "Error %s: %s" (code.ToString()) text'.Result)
