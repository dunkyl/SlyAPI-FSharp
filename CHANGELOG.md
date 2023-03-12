# Changelog

## [Unreleased]

---

## [0.0.8] - 2023-03-11

### Changed
- `Call` Error case is now only `HttpResonseMessage`

### Fixed
- Removed debug prints
- Correctly propogate errors from `IOAuth2.Sign`

## [0.0.7] - 2023-03-11

### Fixed
- JWT serialization

## [0.0.6] - 2023-03-11

Support for Google service accounts in light of changes to user grants for apps in testing.

### Changed
- `Auth` interface renamed to `IAuth`
- `IAuth.Sign` return type is now `Call<HttpRequestMessage>`, and does not throw
- Failed refreshes of credentials during requests will no longer throw, will return the Error case instead

### Added
- `IOAuth2` interface, implemented by `OAuth2` and `OAuth2ServiceAccount`, implements `IAuth`
- `OAuth2ServiceAccounts` to support service accounts authorization

## [0.0.5] - 2023-02-07

### Added
- Serialization.SnakeCaseNamingPolicy

### Fixed
- OAuth2 and WebAPI use SnakeCaseNamingPolicy
- OAuth2App member names "Uri" instead of "Url"

## [0.0.4] - 2023-02-07

### Fixed
- OAuth2 two file path constructor

## [0.0.3] - 2023-02-07

### Added
- Call module
	- `map` and `mapTask` functions
- `SerializedCall` to use serialization but get back a `HttpResponseMessage`
- `GetString` to get string content instead of deserializing as JSON

## [0.0.2] - 2023-02-07

Initial release/fix version