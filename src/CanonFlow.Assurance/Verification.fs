namespace CanonFlow.Assurance.Verification

open System
open System.Text
open System.Text.Json
open CanonFlow.Assurance
open CanonFlow.Assurance.Signing
open FsToolkit.ErrorHandling

module ReceiptVerifier =
    let private validateObject label expectedNames (element: JsonElement) =
        if element.ValueKind <> JsonValueKind.Object then
            Error $"{label} must be an object."
        else
            let actual =
                element.EnumerateObject()
                |> Seq.map (fun property -> property.Name)
                |> Set.ofSeq
            let expected = Set.ofList expectedNames
            if actual = expected then Ok ()
            else
                let missing = Set.difference expected actual |> String.concat ","
                let unexpected = Set.difference actual expected |> String.concat ","
                Error $"{label} fields do not match the receipt schema. Missing=[{missing}] Unexpected=[{unexpected}]."

    let private validateString label (element: JsonElement) =
        if element.ValueKind = JsonValueKind.String && not (isNull (element.GetString())) then Ok ()
        else Error $"{label} must be a string."

    let private validateOptionalString label (element: JsonElement) =
        if element.ValueKind = JsonValueKind.Null || element.ValueKind = JsonValueKind.String then Ok ()
        else Error $"{label} must be a string or null."

    let private validateDigest label (element: JsonElement) =
        result {
            do! validateString label element
            let value = element.GetString()
            if value.StartsWith("sha256:", StringComparison.Ordinal)
               && value.Length = 71
               && value.Substring(7) |> Seq.forall (fun character ->
                   (character >= '0' && character <= '9')
                   || (character >= 'a' && character <= 'f')) then
                return ()
            else
                return! Error $"{label} must be a lowercase sha256 digest."
        }

    let private validateArray label validateItem (element: JsonElement) =
        if element.ValueKind <> JsonValueKind.Array then
            Error $"{label} must be an array."
        else
            element.EnumerateArray()
            |> Seq.mapi (fun index item -> validateItem $"{label}[{index}]" item)
            |> Seq.fold (fun state next ->
                match state, next with
                | Ok (), Ok () -> Ok ()
                | Error error, _ | _, Error error -> Error error) (Ok ())

    let private validateEnum label allowed (element: JsonElement) =
        result {
            do! validateString label element
            let value = element.GetString()
            if allowed |> List.contains value then return ()
            else return! Error $"{label} has unsupported value '{value}'."
        }

    let private validateEvidence label (element: JsonElement) =
        result {
            do! validateObject label ["kind"; "path"; "provenance"; "value"] element
            do! validateString $"{label}.kind" (element.GetProperty("kind"))
            do! validateString $"{label}.path" (element.GetProperty("path"))
            do! validateOptionalString $"{label}.provenance" (element.GetProperty("provenance"))
            do! validateOptionalString $"{label}.value" (element.GetProperty("value"))
        }

    let private validateAssessment label (element: JsonElement) =
        result {
            do!
                validateObject label [
                    "applicableRules"
                    "compliance"
                    "componentId"
                    "componentVersion"
                    "evaluatedRules"
                    "evidence"
                    "health"
                ] element
            do! validateString $"{label}.componentId" (element.GetProperty("componentId"))
            do! validateString $"{label}.componentVersion" (element.GetProperty("componentVersion"))
            do! validateEnum $"{label}.health" ["Complete"; "Partial"; "Broken"] (element.GetProperty("health"))
            do!
                validateEnum
                    $"{label}.compliance"
                    ["Conformant"; "NonConformant"; "NotEstablished"]
                    (element.GetProperty("compliance"))
            let applicable = element.GetProperty("applicableRules")
            let evaluated = element.GetProperty("evaluatedRules")
            match applicable.TryGetInt32(), evaluated.TryGetInt32() with
            | (true, applicableRules), (true, evaluatedRules)
                when applicableRules >= 0 && evaluatedRules >= 0 && evaluatedRules <= applicableRules -> ()
            | _ ->
                return!
                    Error $"{label} rule counts must be non-negative integers with evaluatedRules <= applicableRules."
            do! validateArray $"{label}.evidence" validateEvidence (element.GetProperty("evidence"))
        }

    let private validateEnvelopeSchema (root: JsonElement) =
        result {
            do!
                validateObject "receipt" [
                    "assessments"
                    "context"
                    "evaluator"
                    "receiptType"
                    "replayIdentity"
                    "schemaVersion"
                    "seal"
                    "subject"
                    "verdict"
                ] root
            do! validateEnum "schemaVersion" ["1.0"] (root.GetProperty("schemaVersion"))
            do! validateEnum "receiptType" ["CanonFlowEvidenceReceipt"] (root.GetProperty("receiptType"))
            do! validateDigest "replayIdentity" (root.GetProperty("replayIdentity"))
            do! validateEnum "verdict" ["Pass"; "Inconclusive"; "Fail"; "ToolFailure"] (root.GetProperty("verdict"))

            let subject = root.GetProperty("subject")
            do! validateObject "subject" ["artifacts"; "manifestDigest"; "root"; "schema"; "sourceDirectories"] subject
            do! validateString "subject.root" (subject.GetProperty("root"))
            do! validateString "subject.schema" (subject.GetProperty("schema"))
            do!
                validateArray
                    "subject.sourceDirectories"
                    (fun label value -> validateString label value)
                    (subject.GetProperty("sourceDirectories"))
            let manifestDigest = subject.GetProperty("manifestDigest")
            if manifestDigest.ValueKind <> JsonValueKind.Null then
                do! validateDigest "subject.manifestDigest" manifestDigest
            do!
                validateArray
                    "subject.artifacts"
                    (fun label artifact ->
                        result {
                            do! validateObject label ["digest"; "path"] artifact
                            do! validateString $"{label}.path" (artifact.GetProperty("path"))
                            do! validateDigest $"{label}.digest" (artifact.GetProperty("digest"))
                        })
                    (subject.GetProperty("artifacts"))

            let evaluator = root.GetProperty("evaluator")
            do! validateObject "evaluator" ["engineId"; "engineVersion"] evaluator
            do! validateString "evaluator.engineId" (evaluator.GetProperty("engineId"))
            do! validateString "evaluator.engineVersion" (evaluator.GetProperty("engineVersion"))

            let context = root.GetProperty("context")
            do! validateObject "context" ["instant"; "locale"; "networkPolicy"; "timeProvenance"] context
            do! validateString "context.instant" (context.GetProperty("instant"))
            do! validateString "context.locale" (context.GetProperty("locale"))
            do! validateString "context.networkPolicy" (context.GetProperty("networkPolicy"))
            do! validateString "context.timeProvenance" (context.GetProperty("timeProvenance"))

            do! validateArray "assessments" validateAssessment (root.GetProperty("assessments"))

            let seal = root.GetProperty("seal")
            if seal.ValueKind = JsonValueKind.Null then
                return ()
            else
                do! validateObject "seal" ["algorithm"; "keyId"; "signature"; "status"] seal
                do! validateEnum "seal.status" ["Signed"; "Unsigned"] (seal.GetProperty("status"))
                do! validateOptionalString "seal.algorithm" (seal.GetProperty("algorithm"))
                do! validateOptionalString "seal.keyId" (seal.GetProperty("keyId"))
                do! validateOptionalString "seal.signature" (seal.GetProperty("signature"))
        }

    let rec private fromJsonElement (element: JsonElement) =
        let validateString (value: string) =
            if value |> Seq.exists Char.IsSurrogate then Error "Invalid Unicode surrogate in canonical JSON."
            else Ok value
        match element.ValueKind with
        | JsonValueKind.Null -> Ok JNull
        | JsonValueKind.True -> Ok (JBool true)
        | JsonValueKind.False -> Ok (JBool false)
        | JsonValueKind.String -> validateString (element.GetString()) |> Result.map JString
        | JsonValueKind.Number ->
            match element.TryGetInt32() with
            | true, value -> Ok (JNumber value)
            | _ -> Error "Canonical receipt schema permits only 32-bit integer numbers."
        | JsonValueKind.Array ->
            element.EnumerateArray()
            |> Seq.map fromJsonElement
            |> Seq.fold (fun state next ->
                match state, next with
                | Ok values, Ok value -> Ok (value :: values)
                | Error error, _ | _, Error error -> Error error) (Ok [])
            |> Result.map (List.rev >> JArray)
        | JsonValueKind.Object ->
            let properties = element.EnumerateObject() |> Seq.toList
            let names = properties |> List.map (fun property -> property.Name)
            if names.Length <> (names |> Set.ofList |> Set.count) then
                Error "Duplicate JSON property name."
            else
                properties
                |> List.map (fun property ->
                    result {
                        let! name = validateString property.Name
                        let! value = fromJsonElement property.Value
                        return name, value
                    })
                |> List.fold (fun state next ->
                    match state, next with
                    | Ok values, Ok value -> Ok (value :: values)
                    | Error error, _ | _, Error error -> Error error) (Ok [])
                |> Result.map (List.rev >> JObject)
        | _ -> Error "Unsupported JSON token."

    let verifyCanonicalJson (json: string) =
        try
            use document = JsonDocument.Parse(json, JsonDocumentOptions(MaxDepth = 128))
            fromJsonElement document.RootElement
            |> Result.bind (fun parsed ->
                let canonical = CanonicalReceiptJson.serialize parsed
                if canonical = json then Ok ("sha256:" + Hash.computeSha256 canonical)
                else Error "Receipt is not in canonical form.")
        with ex ->
            Error $"Receipt parsing failed: {ex.Message}"

    let verifyOffline (canonicalPayloadJson: string) (pubKeyBytes: byte[]) (signatureBase64: string) =
        let sigBytes = Convert.FromBase64String(signatureBase64)
        let payloadBytes = Encoding.UTF8.GetBytes(canonicalPayloadJson)

        Ed25519Verify.verify (PublicKey pubKeyBytes) payloadBytes (Signature sigBytes)

    let verifyEnvelopeJson (json: string) (publicKeyBytes: byte[] option) allowUnsigned =
        try
            use document = JsonDocument.Parse(json, JsonDocumentOptions(MaxDepth = 128))
            validateEnvelopeSchema document.RootElement
            |> Result.bind (fun () -> fromJsonElement document.RootElement)
            |> Result.bind (fun parsed ->
                let canonicalEnvelope = CanonicalReceiptJson.serialize parsed
                if canonicalEnvelope <> json then
                    Error "Receipt envelope is not in canonical form."
                else
                    match parsed with
                    | JObject properties ->
                        let seal = document.RootElement.GetProperty("seal")
                        if seal.ValueKind = JsonValueKind.Null then
                            let digest = "sha256:" + Hash.computeSha256 (CanonicalReceiptJson.serialize parsed)
                            if allowUnsigned then Ok digest else Error "Receipt is unsigned."
                        else
                            let status = seal.GetProperty("status").GetString()
                            if status = "Unsigned" then
                                let digest = "sha256:" + Hash.computeSha256 (CanonicalReceiptJson.serialize parsed)
                                if allowUnsigned then Ok digest else Error "Receipt is unsigned."
                            elif status <> "Signed" then
                                Error "Unknown receipt seal status."
                            else
                                match publicKeyBytes with
                                | None -> Error "A public key is required for a signed receipt."
                                | Some publicKey ->
                                    let algorithm = seal.GetProperty("algorithm").GetString()
                                    let signature = seal.GetProperty("signature").GetString()
                                    if algorithm <> "Ed25519" || String.IsNullOrWhiteSpace(signature) then
                                        Error "Signed receipt has invalid seal fields."
                                    else
                                        let signingPayload =
                                            properties
                                            |> List.map (fun (name, value) ->
                                                if name <> "seal" then name, value
                                                else
                                                    match value with
                                                    | JObject sealProperties ->
                                                        name, JObject (sealProperties |> List.map (fun (sealName, sealValue) ->
                                                            if sealName = "signature" then sealName, JNull else sealName, sealValue))
                                                    | _ -> name, value)
                                            |> JObject
                                            |> CanonicalReceiptJson.serialize
                                        let digest = "sha256:" + Hash.computeSha256 signingPayload
                                        verifyOffline signingPayload publicKey signature
                                        |> Result.map (fun () -> digest)
                                        |> Result.mapError (fun error -> $"Receipt signature invalid: {error}")
                    | _ -> Error "Receipt envelope must be a JSON object.")
        with ex ->
            Error $"Receipt envelope parsing failed: {ex.Message}"

    let canonicalDigest (receipt: CanonFlowEvidenceReceipt) =
        receipt
        |> CanonicalReceiptJson.serializeReceipt
        |> Hash.computeSha256
        |> fun hash -> "sha256:" + hash

    let signReceipt keyId privateKey (receipt: CanonFlowEvidenceReceipt) =
        let presealed = {
            receipt with
                Seal = Some {
                    Status = SealStatus.Signed
                    Algorithm = Some SealAlgorithm.Ed25519
                    KeyId = Some keyId
                    Signature = None
                }
        }
        let payload = CanonicalReceiptJson.serializeSigningPayload presealed |> Encoding.UTF8.GetBytes
        let signature = Ed25519Sign.sign privateKey payload |> Convert.ToBase64String
        { presealed with Seal = Some (Seal.createSigned keyId signature) }

    let verifyReceipt pubKeyBytes (receipt: CanonFlowEvidenceReceipt) =
        match receipt.Seal with
        | Some seal when seal.Status = SealStatus.Signed ->
            match seal.Algorithm, seal.Signature with
            | Some SealAlgorithm.Ed25519, Some signature ->
                verifyOffline (CanonicalReceiptJson.serializeSigningPayload receipt) pubKeyBytes signature
            | _ -> Error (InvalidSignatureFormat "Signed receipt is missing Ed25519 seal fields")
        | _ -> Error (InvalidSignatureFormat "Receipt is unsigned")

