namespace Cff.Harness

open System
open System.Globalization
open System.Security.Cryptography
open System.Text
open System.Text.Json
open CanonFlow.Assurance.Contracts

[<RequireQualifiedAccess>]
module Canonical =
    type private Node =
        | Null
        | Boolean of bool
        | Number of decimal
        | Text of string
        | Array of Node list
        | Object of (string * Node) list

    let private normalize (value: string) =
        if isNull value then null else value.Normalize(NormalizationForm.FormC)

    let private escape (value: string) =
        JsonSerializer.Serialize(normalize value)

    let rec private render = function
        | Null -> "null"
        | Boolean true -> "true"
        | Boolean false -> "false"
        | Number value -> value.ToString("0.############################", CultureInfo.InvariantCulture)
        | Text value -> escape value
        | Array values -> values |> List.map render |> String.concat "," |> fun body -> "[" + body + "]"
        | Object properties ->
            properties
            |> List.sortWith (fun (left, _) (right, _) -> String.CompareOrdinal(left, right))
            |> List.map (fun (name, value) -> escape name + ":" + render value)
            |> String.concat ","
            |> fun body -> "{" + body + "}"

    let rec private fromElement (element: JsonElement) =
        match element.ValueKind with
        | JsonValueKind.Null -> Ok Null
        | JsonValueKind.True -> Ok (Boolean true)
        | JsonValueKind.False -> Ok (Boolean false)
        | JsonValueKind.String -> Ok (Text (element.GetString()))
        | JsonValueKind.Number ->
            match element.TryGetDecimal() with
            | true, number -> Ok (Number number)
            | _ -> Error "JSON number is outside the admitted invariant decimal domain"
        | JsonValueKind.Array ->
            let folder state child =
                match state, fromElement child with
                | Ok values, Ok value -> Ok (value :: values)
                | Error error, _ | _, Error error -> Error error
            element.EnumerateArray()
            |> Seq.fold folder (Ok [])
            |> Result.map (List.rev >> Array)
        | JsonValueKind.Object ->
            let folder state (property: JsonProperty) =
                match state with
                | Error error -> Error error
                | Ok (names, properties) when Set.contains property.Name names ->
                    Error ("Duplicate JSON property: " + property.Name)
                | Ok (names, properties) ->
                    match fromElement property.Value with
                    | Ok value ->
                        Ok (
                            Set.add property.Name names,
                            (normalize property.Name, value) :: properties
                        )
                    | Error error -> Error error
            element.EnumerateObject()
            |> Seq.fold folder (Ok (Set.empty, []))
            |> Result.map (snd >> List.rev >> Object)
        | kind -> Error ("Unsupported JSON token: " + string kind)

    let canonicalizeJson (bytes: byte array) =
        try
            use document =
                JsonDocument.Parse(
                    ReadOnlyMemory<byte>(bytes),
                    JsonDocumentOptions(
                        AllowTrailingCommas = false,
                        CommentHandling = JsonCommentHandling.Disallow,
                        MaxDepth = 64
                    )
                )
            fromElement document.RootElement
            |> Result.map (render >> Encoding.UTF8.GetBytes)
        with
        | :? JsonException as error -> Error ("Malformed JSON: " + error.Message)

    let sha256Bytes (bytes: byte array) =
        let hexadecimal = SHA256.HashData(bytes) |> Convert.ToHexString
        ContentDigest.createSha256 ("sha256:" + hexadecimal.ToLowerInvariant())
        |> Result.defaultWith invalidOp

    let sha256Text (value: string) =
        value |> normalize |> Encoding.UTF8.GetBytes |> sha256Bytes

    let verifyDigest expected bytes =
        sha256Bytes bytes = expected

    let private objectJson properties = Object properties |> render
    let private text value = Text value
    let private optional mapping = function Some value -> mapping value | None -> Null

    let sourceClause (source: SourceClause) =
        objectJson [
            "admittedAt", text (source.AdmittedAt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture))
            "admittedBy", text source.AdmittedBy
            "clauseId", text (ClauseId.value source.ClauseId)
            "documentId", text source.Locator.DocumentId
            "effectiveFrom", source.EffectiveFrom |> optional (fun value -> text (value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)))
            "extractDigest", text (ContentDigest.value source.ExtractDigest)
            "interpretationNote", text source.InterpretationNote
            "section", text source.Locator.Section
            "sourceDigest", text (ContentDigest.value source.SourceDigest)
            "sourceKind", text (string source.SourceKind)
            "supersedes", source.Supersedes |> optional (ClauseId.value >> text)
            "uri", source.Locator.Uri |> optional text
            "version", text source.Locator.Version
        ]

    let rec private applicabilityNode = function
        | Always -> Object [ "case", Text "Always" ]
        | ProfileIs value -> Object [ "case", Text "ProfileIs"; "value", Text (ProfileId.value value) ]
        | DomainIs value -> Object [ "case", Text "DomainIs"; "value", Text value ]
        | VersionIs value -> Object [ "case", Text "VersionIs"; "value", Text value ]
        | RoleIs value -> Object [ "case", Text "RoleIs"; "value", Text value ]
        | FlowIs value -> Object [ "case", Text "FlowIs"; "value", Text value ]
        | ActionPresent value -> Object [ "case", Text "ActionPresent"; "value", Text value ]
        | FactEquals (path, value) ->
            let fact =
                match value with
                | FactValue.Text value -> Object [ "case", Text "Text"; "value", Text value ]
                | FactValue.Number value -> Object [ "case", Text "Number"; "value", Number value ]
                | FactValue.Boolean value -> Object [ "case", Text "Boolean"; "value", Boolean value ]
            Object [ "case", Text "FactEquals"; "path", Text (FactPath.value path); "value", fact ]
        | AllOf values -> Object [ "case", Text "AllOf"; "values", values |> List.map applicabilityNode |> Array ]
        | AnyOf values -> Object [ "case", Text "AnyOf"; "values", values |> List.map applicabilityNode |> Array ]
        | Not value -> Object [ "case", Text "Not"; "value", applicabilityNode value ]

    let private evidenceKind = function
        | ProtocolMessage action -> Object [ "case", Text "ProtocolMessage"; "action", Text action ]
        | PairedMessage (request, callback) ->
            Object [ "case", Text "PairedMessage"; "requestAction", Text request; "callbackAction", Text callback ]
        | value -> Object [ "case", Text (string value) ]

    let private requirementNode requirement =
        Object [
            "cardinality", Text (string requirement.Cardinality)
            "description", Text requirement.Description
            "kind", evidenceKind requirement.Kind
            "requirementId", Text requirement.RequirementId
            "trust", Text (string requirement.Trust)
        ]

    let obligationDefinition definition =
        objectJson [
            "applicability", applicabilityNode definition.Applicability
            "authority", sourceClause definition.Authority |> JsonDocument.Parse |> fun document -> fromElement document.RootElement |> Result.defaultWith invalidOp
            "evaluatorId", text (EvaluatorId.value definition.EvaluatorId)
            "requiredEvidence", definition.RequiredEvidence |> List.map requirementNode |> Array
            "ruleId", text (RuleId.value definition.RuleId)
            "ruleVersion", Number (decimal definition.RuleVersion)
            "supportingAuthorities",
                definition.SupportingAuthorities
                |> List.map (sourceClause >> Encoding.UTF8.GetBytes >> fun bytes ->
                    use document = JsonDocument.Parse(bytes)
                    fromElement document.RootElement |> Result.defaultWith invalidOp)
                |> Array
            "title", text definition.Title
        ]

    let obligationDigest definition =
        definition |> obligationDefinition |> sha256Text

    let evidenceManifest (bundle: EvidenceBundle) =
        let itemNode = function
            | Message message ->
                Object [
                    "action", Text message.Action
                    "canonicalPayloadDigest", message.CanonicalPayloadDigest |> optional (ContentDigest.value >> Text)
                    "capturedAt", Text (message.Provenance.CapturedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))
                    "capturedBy", Text message.Provenance.CapturedBy
                    "captureMethod", Text (string message.Provenance.CaptureMethod)
                    "counterpartyId", message.Correlation.CounterpartyId |> optional Text
                    "kind", Text "Message"
                    "messageId", Text message.Correlation.MessageId
                    "rawPayloadDigest", Text (ContentDigest.value message.RawPayloadDigest)
                    "subscriberId", message.Correlation.SubscriberId |> optional Text
                    "timestamp", Text (message.Timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))
                    "transactionId", Text message.Correlation.TransactionId
                    "trust", Text (string message.Provenance.EstablishedTrust)
                ]
            | Registry registry ->
                Object [
                    "kind", Text "Registry"
                    "keyId", Text registry.KeyId
                    "observationDigest", Text (ContentDigest.value registry.ObservationDigest)
                    "subscriberId", Text registry.SubscriberId
                ]
            | Observation observation ->
                Object [
                    "capturedAt", Text (observation.Provenance.CapturedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))
                    "capturedBy", Text observation.Provenance.CapturedBy
                    "captureMethod", Text (string observation.Provenance.CaptureMethod)
                    "evidenceKind", evidenceKind observation.Kind
                    "kind", Text "Observation"
                    "observationDigest", Text (ContentDigest.value observation.ObservationDigest)
                    "trust", Text (string observation.Provenance.EstablishedTrust)
                ]
        objectJson [
            "bundleId", Text bundle.BundleId
            "items", bundle.Items |> List.map itemNode |> Array
            "profile", Text (ProfileId.value bundle.Profile)
        ]

    let evidenceBundleDigest bundle =
        bundle |> evidenceManifest |> sha256Text

    let rulePack definition =
        let obligations =
            definition.Obligations
            |> List.sortBy (fun item -> RuleId.value item.RuleId, item.RuleVersion)
            |> List.map (obligationDigest >> ContentDigest.value >> Text)
            |> Array
        objectJson [
            "aggregationPolicyDigest", Text (ContentDigest.value definition.AggregationPolicyDigest)
            "canonicalizationProfileDigest", Text (ContentDigest.value definition.CanonicalizationProfileDigest)
            "obligations", obligations
            "profile", Text (ProfileId.value definition.Profile)
            "rulePackId", Text (RulePackId.value definition.RulePackId)
            "sourceProfileDigest", Text (ContentDigest.value definition.SourceProfileDigest)
            "supersedes", definition.Supersedes |> optional (ContentDigest.value >> Text)
            "version", Number (decimal definition.Version)
        ]

    let rulePackDigest definition = definition |> rulePack |> sha256Text

    let factsDigest facts =
        facts
        |> List.sortBy (fun fact -> FactPath.value fact.Path)
        |> List.map (fun fact ->
            let value =
                match fact.Value with
                | FactValue.Text value -> "text:" + value
                | FactValue.Number value -> "number:" + value.ToString(CultureInfo.InvariantCulture)
                | FactValue.Boolean value -> "boolean:" + string value
            FactPath.value fact.Path + "=" + value)
        |> String.concat "\n"
        |> sha256Text
