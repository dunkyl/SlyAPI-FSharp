/// Implementation for generating JWT tokens for Google web service accounts
module net.dunkyl.SlyAPI.Jwt

open System.Security.Cryptography
open System.Text.Json
open System.Text.Json.Serialization
open System.Text
open System
open Tokens

module Components =
    type Header = {
        alg: string
        typ: string
    }

    type Claim = {
        iss: string // issued to email
        scope: string // scope of the token
        aud: string // audience of the token
        exp: int64 // expiration
        iat: int64 // issued at
    }

    type AdditionalClaim = {
        iss: string
        sub: string
        scope: string
        aud: string
        exp: int64
        iat: int64
    }

open Components

let private encodeJwt (header: Header) (claim: Claim) (alg: HashAlgorithm) (key: RSACryptoServiceProvider) =
    let headerJson = JsonSerializer.Serialize(header)
    let claimJson = JsonSerializer.Serialize(claim)
    let header64 = headerJson |> urlSafeEncode64
    let claim64 = claimJson |> urlSafeEncode64
    let headerClaim = header64 + "." + claim64
    let signature = key.SignData(Encoding.UTF8.GetBytes headerClaim, alg)
    headerClaim + "." + urlSafeEncode64 signature

let googleJsonToJwt scope (pem: string) email =
    let googleHeader = {
        alg = "RS256"
        typ = "JWT"
    }
    let private_key = pem
    let rsa = new RSACryptoServiceProvider()
    rsa.ImportFromPem(private_key)
    let alg = SHA256.Create()
    let nowStamp = DateTimeOffset.Now.ToUnixTimeSeconds()
    let expiryStamp = nowStamp + 1800L
    let claim = {
        iss = email
        scope = scope
        aud = "https://oauth2.googleapis.com/token"
        exp = expiryStamp
        iat = nowStamp
    }
    encodeJwt googleHeader claim alg rsa