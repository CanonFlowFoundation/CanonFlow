namespace CanonFlow.Assurance.Signing

open System
open Org.BouncyCastle.Crypto.Parameters
open Org.BouncyCastle.Crypto.Signers

type PrivateKey = private PrivateKey of byte[]

module PrivateKey =
    let create (bytes: byte[]) =
        if isNull bytes || bytes.Length <> 32 then
            Error "Ed25519 private seed must be exactly 32 bytes"
        else
            Ok (PrivateKey (Array.copy bytes))

    let internal bytes (PrivateKey value) = Array.copy value

module Ed25519Sign =
    let sign privateKey (message: byte[]) =
        let key = Ed25519PrivateKeyParameters(PrivateKey.bytes privateKey, 0)
        let signer = Ed25519Signer()
        signer.Init(true, key)
        signer.BlockUpdate(message, 0, message.Length)
        signer.GenerateSignature() |> Array.copy
