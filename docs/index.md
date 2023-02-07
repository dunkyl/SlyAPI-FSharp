# Welcome to SlyAPI F#!

> 🚧 **This library is an early work in progress! Breaking changes may be frequent.**

This is a base library used by some of my other projects for wrapping web APIs.
Currently, that's just the following:

 - SlyDiscord (TODO: Link)

The goal of this library is to provide easy and readible models for creating higher-level libraries. It is also [available in Python!](https://github.com/dunkyl/SlyAPI-Python).

## Features

- Easy to use models for creating API wrappers.
- Automatic serialization and deserialization of JSON data.
- OAuth2 support.
- Async!

## Installation

The package is available on NuGet. You can add it your project with the following command:
```
dotnet add package net.dunkyl.SlyAPI
```

<div class="admonition info">
 <h3>Before you continue</h3>
 <p>This package does <b>not</b> implement any particular API. If you are looking to just use an API, you should look at the packages that use this library or recommendations for the API's documentation.
</div>

## Basic Usage

To get started, subclass the `net.dunkyl.SlyAPI.WebAPI` type and implement it's `BaseURL` and `UserAgent` properties.
This example uses the [OpenWeatherMap API](https://openweathermap.org/), and implements just one endpoint.

```fsharp
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
```

For each endpoint I reccomend the use of tupled, rather than curried, arguments. Some APIs have overloaded endpoints and C# can't use curried form as ergonomically.

Happy coding!