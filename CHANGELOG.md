# Changelog

## [Unreleased]

## [0.0.5] - 2023-02-07

### Added

- Serialization.SnakeCaseNamingPolicy

### Fixed

- OAuth2 and WebAPI use SnakeCaseNamingPolicy

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