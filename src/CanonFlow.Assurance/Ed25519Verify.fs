namespace CanonFlow.Assurance.Verification

open System
open Org.BouncyCastle.Crypto.Parameters
open Org.BouncyCastle.Crypto.Signers

type PublicKey = PublicKey of byte[]
type Signature = Signature of byte[]

type VerifyError =
    | InvalidKeyFormat of string
    | InvalidSignatureFormat of string
    | VerificationFailed

module Ed25519Verify =

    let verify (PublicKey pubKeyBytes) (message: byte[]) (Signature sigBytes) : Result<unit, VerifyError> =
        if pubKeyBytes.Length <> 32 then
            Error (InvalidKeyFormat "Ed25519 public key must be exactly 32 bytes")
        elif sigBytes.Length <> 64 then
            Error (InvalidSignatureFormat "Ed25519 signature must be exactly 64 bytes")
        else
            try
                let pubKeyParams = new Ed25519PublicKeyParameters(pubKeyBytes, 0)
                let verifier = new Ed25519Signer()
                verifier.Init(false, pubKeyParams)
                verifier.BlockUpdate(message, 0, message.Length)
                let isValid = verifier.VerifySignature(sigBytes)
                if isValid then Ok () else Error VerificationFailed
            with
            | ex -> Error (InvalidKeyFormat ex.Message)
