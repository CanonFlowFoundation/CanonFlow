namespace CanonFlow.Assurance

open System
open System.Text

type SealAlgorithm = 
    | Ed25519

type SealStatus =
    | Signed
    | Unsigned

type ReceiptSeal = {
    Status: SealStatus
    Algorithm: SealAlgorithm option
    KeyId: string option
    Signature: string option
}

module Seal =
    
    let createUnsigned () =
        { Status = SealStatus.Unsigned
          Algorithm = None
          KeyId = None
          Signature = None }

    let createSigned keyId signatureBase64 =
        { Status = SealStatus.Signed
          Algorithm = Some SealAlgorithm.Ed25519
          KeyId = Some keyId
          Signature = Some signatureBase64 }

