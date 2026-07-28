namespace CanonFlow.Assurance

open System
open System.Text
open System.Text.Json
open FsToolkit.ErrorHandling

type ObligationManifest =
    private ObligationManifest of Digest * NonEmpty<Obligation>

[<RequireQualifiedAccess>]
module ObligationManifest =
    [<Literal>]
    let SchemaVersion = "1.0"

    [<Literal>]
    let ManifestType = "CanonFlowObligationManifest"

    let create policyDigest obligations =
        match NonEmpty.ofList obligations with
        | Error _ -> Error "An obligation manifest must contain at least one obligation."
        | Ok nonEmptyObligations ->
            let duplicate =
                nonEmptyObligations
                |> NonEmpty.toList
                |> List.countBy (Obligation.id >> ObligationId.value)
                |> List.tryFind (fun (_, count) -> count > 1)
            match duplicate with
            | Some (obligationId, _) ->
                Error $"Obligation '{obligationId}' is duplicated."
            | None ->
                Ok (ObligationManifest (policyDigest, nonEmptyObligations))

    let policyDigest (ObligationManifest (value, _)) = value
    let obligations (ObligationManifest (_, value)) = value

    let private encodeGate gate =
        JObject [
            "id", JString (gate |> ProofGateReference.gateId |> ProofGateId.value)
            "implementationDigest", JString (gate |> ProofGateReference.implementationDigest |> Digest.toString)
            "version", JString (gate |> ProofGateReference.version)
        ]

    let private encodeDerivation = function
        | ProjectionDerivation.None ->
            JObject [
                "kind", JString "None"
            ]
        | ProjectionDerivation.Candidate assumptions ->
            JObject [
                "assumptionIds",
                    assumptions
                    |> NonEmpty.toList
                    |> List.map AssumptionId.value
                    |> List.sort
                    |> List.map JString
                    |> JArray
                "kind", JString "Candidate"
            ]
        | ProjectionDerivation.Admitted admissionId ->
            JObject [
                "admissionId", JString (AdmissionId.value admissionId)
                "kind", JString "Admitted"
            ]
        | ProjectionDerivation.Unsupported reasonId ->
            JObject [
                "kind", JString "Unsupported"
                "reasonId", JString (UnsupportedReasonId.value reasonId)
            ]

    let private projectionStateText = function
        | ProjectionState.Dormant -> "Dormant"
        | ProjectionState.CandidateRequiringApproval -> "CandidateRequiringApproval"
        | ProjectionState.Admitted -> "Admitted"
        | ProjectionState.Unsupported -> "Unsupported"

    let private encodeObligation obligation =
        JObject [
            "id", JString (obligation |> Obligation.id |> ObligationId.value)
            "normalizedPredicateDigest",
                JString (
                    obligation
                    |> Obligation.normalizedPredicateDigest
                    |> Digest.toString
                )
            "projection",
                JObject [
                    "derivation", obligation |> Obligation.derivation |> encodeDerivation
                    "state",
                        obligation
                        |> Obligation.projectionState
                        |> projectionStateText
                        |> JString
                ]
            "requiredGates",
                obligation
                |> Obligation.requiredGates
                |> NonEmpty.toList
                |> List.sortBy (ProofGateReference.gateId >> ProofGateId.value)
                |> List.map encodeGate
                |> JArray
            "sourceDigest",
                JString (obligation |> Obligation.sourceDigest |> Digest.toString)
        ]

    let private payloadProperties manifest =
        [
            "manifestType", JString ManifestType
            "obligations",
                manifest
                |> obligations
                |> NonEmpty.toList
                |> List.sortBy (Obligation.id >> ObligationId.value)
                |> List.map encodeObligation
                |> JArray
            "policyDigest", JString (manifest |> policyDigest |> Digest.toString)
            "schemaVersion", JString SchemaVersion
        ]

    let private encodePayload manifest =
        manifest |> payloadProperties |> JObject

    let protectedDigest manifest =
        manifest
        |> encodePayload
        |> CanonicalReceiptJson.serialize
        |> Digest.sha256Text

    let serialize manifest =
        JObject (
            ("protectedDigest", JString (manifest |> protectedDigest |> Digest.toString))
            :: payloadProperties manifest
        )
        |> CanonicalReceiptJson.serialize

    let private validateObject label expectedNames (element: JsonElement) =
        if element.ValueKind <> JsonValueKind.Object then
            Error $"{label} must be an object."
        else
            let properties = element.EnumerateObject() |> Seq.toList
            let actualNames = properties |> List.map (fun property -> property.Name)
            let actual = actualNames |> Set.ofList
            let expected = expectedNames |> Set.ofList
            if actualNames.Length <> actual.Count then
                Error $"{label} contains a duplicate field."
            elif actual = expected then
                Ok ()
            else
                let missing = Set.difference expected actual |> String.concat ","
                let unexpected = Set.difference actual expected |> String.concat ","
                Error $"{label} fields do not match the schema. Missing=[{missing}] Unexpected=[{unexpected}]."

    let private stringValue label (element: JsonElement) =
        if element.ValueKind <> JsonValueKind.String then
            Error $"{label} must be a string."
        else
            let value = element.GetString()
            if isNull value || value |> Seq.exists Char.IsSurrogate then
                Error $"{label} contains invalid Unicode."
            else
                Ok value

    let private arrayValues label (element: JsonElement) =
        if element.ValueKind <> JsonValueKind.Array then
            Error $"{label} must be an array."
        else
            Ok (element.EnumerateArray() |> Seq.toList)

    let private traverse parse values =
        values
        |> List.fold (fun state value ->
            match state, parse value with
            | Ok parsed, Ok item -> Ok (item :: parsed)
            | Error error, _ | _, Error error -> Error error) (Ok [])
        |> Result.map List.rev

    let private digestValue label element =
        result {
            let! value = stringValue label element
            return! Digest.parse value
        }

    let private parseIdentifier label create element =
        result {
            let! value = stringValue label element
            return! create value
        }

    let private parseGate index (element: JsonElement) =
        let label = $"requiredGates[{index}]"
        result {
            do! validateObject label ["id"; "implementationDigest"; "version"] element
            let! gateId =
                parseIdentifier
                    $"{label}.id"
                    ProofGateId.create
                    (element.GetProperty("id"))
            let! implementationDigest =
                digestValue
                    $"{label}.implementationDigest"
                    (element.GetProperty("implementationDigest"))
            let! version =
                stringValue
                    $"{label}.version"
                    (element.GetProperty("version"))
            return! ProofGateReference.create gateId version implementationDigest
        }

    let private parseDerivation (element: JsonElement) =
        result {
            if element.ValueKind <> JsonValueKind.Object then
                return! Error "projection.derivation must be an object."
            let! kind =
                if element.TryGetProperty("kind") |> fst then
                    stringValue "projection.derivation.kind" (element.GetProperty("kind"))
                else
                    Error "projection.derivation.kind is required."
            match kind with
            | "None" ->
                do! validateObject "projection.derivation" ["kind"] element
                return ProjectionDerivation.None
            | "Candidate" ->
                do! validateObject "projection.derivation" ["assumptionIds"; "kind"] element
                let! values =
                    arrayValues
                        "projection.derivation.assumptionIds"
                        (element.GetProperty("assumptionIds"))
                let! assumptions =
                    values
                    |> List.mapi (fun index value -> index, value)
                    |> traverse (fun (index, value) ->
                        parseIdentifier
                            $"projection.derivation.assumptionIds[{index}]"
                            AssumptionId.create
                            value)
                match NonEmpty.ofList assumptions with
                | Error _ ->
                    return! Error "A candidate derivation must contain at least one assumption identifier."
                | Ok nonEmpty ->
                    return ProjectionDerivation.Candidate nonEmpty
            | "Admitted" ->
                do! validateObject "projection.derivation" ["admissionId"; "kind"] element
                let! admissionId =
                    parseIdentifier
                        "projection.derivation.admissionId"
                        AdmissionId.create
                        (element.GetProperty("admissionId"))
                return ProjectionDerivation.Admitted admissionId
            | "Unsupported" ->
                do! validateObject "projection.derivation" ["kind"; "reasonId"] element
                let! reasonId =
                    parseIdentifier
                        "projection.derivation.reasonId"
                        UnsupportedReasonId.create
                        (element.GetProperty("reasonId"))
                return ProjectionDerivation.Unsupported reasonId
            | unsupported ->
                return! Error $"projection.derivation.kind '{unsupported}' is unsupported."
        }

    let private parseProjection (element: JsonElement) =
        result {
            do! validateObject "projection" ["derivation"; "state"] element
            let! state =
                stringValue "projection.state" (element.GetProperty("state"))
            let! derivation =
                parseDerivation (element.GetProperty("derivation"))
            let expectedState =
                derivation
                |> ProjectionDerivation.state
                |> projectionStateText
            if state <> expectedState then
                return!
                    Error $"projection.state must be '{expectedState}' for its structured derivation."
            return derivation
        }

    let private parseObligation index (element: JsonElement) =
        let label = $"obligations[{index}]"
        result {
            do!
                validateObject label [
                    "id"
                    "normalizedPredicateDigest"
                    "projection"
                    "requiredGates"
                    "sourceDigest"
                ] element
            let! obligationId =
                parseIdentifier
                    $"{label}.id"
                    ObligationId.create
                    (element.GetProperty("id"))
            let! sourceDigest =
                digestValue
                    $"{label}.sourceDigest"
                    (element.GetProperty("sourceDigest"))
            let! predicateDigest =
                digestValue
                    $"{label}.normalizedPredicateDigest"
                    (element.GetProperty("normalizedPredicateDigest"))
            let! gateValues =
                arrayValues
                    $"{label}.requiredGates"
                    (element.GetProperty("requiredGates"))
            let! gates =
                gateValues
                |> List.mapi (fun gateIndex gate -> gateIndex, gate)
                |> traverse (fun (gateIndex, gate) -> parseGate gateIndex gate)
            let! derivation =
                parseProjection (element.GetProperty("projection"))
            return!
                Obligation.create
                    obligationId
                    sourceDigest
                    predicateDigest
                    gates
                    derivation
        }

    let parseBytes (bytes: byte[]) =
        if isNull bytes then
            Error "Obligation manifest bytes must not be null."
        else
            try
                let utf8 = UTF8Encoding(false, true)
                let json = utf8.GetString(bytes)
                use document =
                    JsonDocument.Parse(
                        json,
                        JsonDocumentOptions(
                            AllowTrailingCommas = false,
                            CommentHandling = JsonCommentHandling.Disallow,
                            MaxDepth = 64
                        )
                    )
                let root = document.RootElement
                result {
                    do!
                        validateObject "manifest" [
                            "manifestType"
                            "obligations"
                            "policyDigest"
                            "protectedDigest"
                            "schemaVersion"
                        ] root
                    let! schemaVersion =
                        stringValue "schemaVersion" (root.GetProperty("schemaVersion"))
                    if schemaVersion <> SchemaVersion then
                        return! Error $"Unsupported obligation manifest schemaVersion '{schemaVersion}'."
                    let! manifestType =
                        stringValue "manifestType" (root.GetProperty("manifestType"))
                    if manifestType <> ManifestType then
                        return! Error $"Unsupported manifestType '{manifestType}'."
                    let! policyDigest =
                        digestValue "policyDigest" (root.GetProperty("policyDigest"))
                    let! obligationValues =
                        arrayValues "obligations" (root.GetProperty("obligations"))
                    let! parsedObligations =
                        obligationValues
                        |> List.mapi (fun index obligation -> index, obligation)
                        |> traverse (fun (index, obligation) -> parseObligation index obligation)
                    let! manifest = create policyDigest parsedObligations
                    let! declaredProtectedDigest =
                        digestValue
                            "protectedDigest"
                            (root.GetProperty("protectedDigest"))
                    if
                        Digest.toString declaredProtectedDigest
                        <> (manifest |> protectedDigest |> Digest.toString)
                    then
                        return! Error "protectedDigest does not match the canonical manifest payload."
                    if serialize manifest <> json then
                        return! Error "Obligation manifest is not in canonical form."
                    return manifest
                }
            with ex ->
                Error $"Obligation manifest parsing failed: {ex.Message}"
