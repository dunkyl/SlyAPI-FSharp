# ![sly logo](https://raw.githubusercontent.com/dunkyl/SlyMeta/main/sly%20logo%20f%23.svg) SlyAPI for F#

<!-- elevator begin -->

> 🚧 **This library is an early work in progress! Breaking changes may be frequent.**

> 🟣 For .NET 7+

No-boilerplate, async web api access with oauth2.

```shell
dotnet add package net.dunkyl.SlyAPI
```

Meant as a foundation for other libraries more than being used directly.

This is the F# version of a [Python package of the same name](https://github.com/dunkyl/SlyAPI-Python)

The library currently is only in a minimal state of features.

<!-- elevator end -->

---

Example library usage:

```py
open System
open net.dunkyl.SlyAPI

type Units = Standard (* Kelvin *) | Imperial | Metric

type CityWeather = {
    Name: string
    Main: {| Temp: float |}
    Weather: {| Description: string |} list
}

type OpenWeather (key: string) =
    inherit WebAPI(QueryAPIKey("appid", key))
    
    override _.BaseURL = Uri "https://api.openweathermap.org/data/2.5"
    override _.UserAgent = "YourWeatherAppLibrary/0.99.0"

    /// Get the current weather of a city.
    /// Location format: `City,Country` or `City,State,Country`
    /// where State and Country are ISO3166 codes.
    member this.City (location: string, units: Units): CityWeather Call =
        this.Get (urlQuery "weather" [ "units", units; "q", location ]) ()

    // ...
```
Happy coding!