---
title: Overview
category: Documentaion
categoryindex: 1
index: 1
---

# Overview

## Prerequisites

 - .NET 7.0 SDK
 - C# or F# (F# is recommended)

## Key Types

### HTTP Messages
`cref:T:System.Net.Http.HttpRequestMessage` and `cref:T:System.Net.Http.HttpResponseMessage`
The .NET built-in types for HTTP requests and responses. There are no special wrappers.
Sent to and recieved from `cref:T:System.Net.Http.HttpClient`.

### Auth Interface
`cref:T:net.dunkyl.SlyAPI.Auth`

Interface for any authentication scheme. Adds whatever is required to a request for it to be authenticated.
This library comes with four implementations of it:

 - `cref:T:net.dunkyl.SlyAPI.NoAuth` - does nothing
 - `cref:T:net.dunkyl.SlyAPI.HeaderAPIKey` - adds a header at a particular key with the API key as the value
 - `cref:T:net.dunkyl.SlyAPI.QueryAPIKey` - adds a query parameter at a particular key with the API key as the value
 - `cref:T:net.dunkyl.SlyAPI.OAuth2` - adds OAuth2 headers and refreshes the token when it expires
 
Since OAuth2 has more steps to use, it is explained in its own section: [OAuth2](oauth2.md).

### Base WebAPI Class
`cref:T:net.dunkyl.SlyAPI.WebAPI`

Abstract class. Implementers should provide each endpoint as a method.

## `Call<'T>`
Type alias for `Task<Result<'T, Tuple<HttpStatusCode, HttpResponseMessage>>>`. Represents a dispatched, async request to the API that will be deserialized to a type `'T`.

`Task` is the ordinary one from `System.Threading.Tasks`.

`Result` is the ordinary one from `FSharp.Core`.

## Key Methods

### Serialized Requests
```cref:M:net.dunkyl.SlyAPI.WebAPI.Get``2```, `Post`, `Put`, etc

One for each HTTP method, these do automatic JSON serialization and deserializtion. Making `'In` be the `Unit` type will skip serializtion and send the request without data. `'Out` is the type that the response will be deserialized to. Making it `Unit` will skip deserialization and expect an empty response. To pass query parameters, add them to the `path` paramter with  the `urlQuery` function which will encode them.

### HTTP Message Requests
`cref:M:net.dunkyl.SlyAPI.WebAPI.RawRequest`

If a particular endpoint needs special values in headers or some other specific consideration, this method can be used to send a request with a custom `HttpRequestMessage` and get a `Call<HttpResponseMessage>` back. It only applies status code checking and authorization.

### Unwrap Extension
[FSharp.Core.Result<'T, ...>.Unwrap()](net-dunkyl-slyapi-extensions.html)

For better compatibility with C# when using exceptions, an extension method is provided to raise API errors as `cref:T:net.dunkyl.SlyAPI.Extensions.APIException`.