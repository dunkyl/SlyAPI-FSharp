/// Tokens for OAuth2 PKCE, JWT, etc.
module net.dunkyl.SlyAPI.Tokens

open System
open System.Security.Cryptography
open System.Text

let private rng = RandomNumberGenerator.Create()

[<AutoOpen>]
type Base64 =

    /// Base-64 encoding with the url safe digit set
    /// https://www.rfc-editor.org/rfc/rfc4648#section-5
    static member urlSafeEncode64 bytes =
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')

    static member urlSafeEncode64 (text: string) =
        let bytes = Encoding.UTF8.GetBytes(text)
        bytes |> Base64.urlSafeEncode64

/// Make a new string token of the length
/// For OAuth2 PKCE, the number of bytes is at least 32 (43 digits)
/// and at most 96 (128 digits)
/// https://www.rfc-editor.org/rfc/rfc7636#section-4.1
let tokenUrlSafe length =
    let bytes = Array.zeroCreate<byte> length
    rng.GetBytes(bytes)
    urlSafeEncode64 bytes
    
/// Url-safe-base-64-encoded hash of a token
let sha256ofToken (token: string) =
    token
    |> Encoding.UTF8.GetBytes
    |> SHA256.Create().ComputeHash
    |> urlSafeEncode64