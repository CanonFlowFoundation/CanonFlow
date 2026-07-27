namespace CanonFlow.Assurance

open System

type Digest = private Digest of byte[]

module Digest =
    let create (bytes: byte[]) =
        if isNull bytes || bytes.Length <> 32 then
            Error "Digest must be exactly 32 bytes"
        else
            let copy = Array.zeroCreate bytes.Length
            Array.Copy(bytes, copy, bytes.Length)
            Ok (Digest copy)

    let toBytes (Digest bytes) =
        let copy = Array.zeroCreate bytes.Length
        Array.Copy(bytes, copy, bytes.Length)
        copy

    let toString (Digest bytes) =
        "sha256:" + BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant()

