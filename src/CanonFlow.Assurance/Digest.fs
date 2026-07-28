namespace CanonFlow.Assurance

open System
open System.Text

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

    let parse (value: string) =
        if isNull value
           || value.Length <> 71
           || not (value.StartsWith("sha256:", StringComparison.Ordinal)) then
            Error "Digest must use canonical lowercase sha256:<64-hex> form"
        else
            let hexadecimal = value.Substring(7)
            if hexadecimal
               |> Seq.forall (fun character ->
                   (character >= '0' && character <= '9')
                   || (character >= 'a' && character <= 'f')) then
                try
                    Convert.FromHexString(hexadecimal) |> create
                with _ ->
                    Error "Digest contains invalid hexadecimal"
            else
                Error "Digest must contain lowercase hexadecimal"

    let sha256Bytes bytes =
        bytes |> Hash.computeSha256Bytes |> Digest

    let sha256Text (value: string) =
        value |> Encoding.UTF8.GetBytes |> sha256Bytes

