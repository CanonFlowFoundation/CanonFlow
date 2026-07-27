namespace CanonFlow.Assurance

open System
open System.Text

type SealAlgorithm = 
    | Ed25519

type SealStatus =
    | Signed
    | Unsigned

type ReceiptSeal = {
    Status: string
    Algorithm: string option
    KeyId: string option
    Signature: string option
}

module Seal =
    
    let createUnsigned () =
        { Status = "Unsigned"
          Algorithm = None
          KeyId = None
          Signature = None }

    let createSigned keyId signatureBase64 =
        { Status = "Signed"
          Algorithm = Some "Ed25519"
          KeyId = Some keyId
          Signature = Some signatureBase64 }

