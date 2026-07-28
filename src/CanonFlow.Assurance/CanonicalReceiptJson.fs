namespace CanonFlow.Assurance

open System
open System.Text
open System.Globalization

type CanonicalJson =
    | JString of string
    | JBool   of bool
    | JNumber of int
    | JNull
    | JArray  of CanonicalJson list
    | JObject of (string * CanonicalJson) list

module CanonicalReceiptJson =
    
    // RFC 8785 §3.2.2.2 minimal string escaping
    let private escapeString (s: string) =
        let sb = StringBuilder()
        sb.Append('"') |> ignore
        for c in s do
            match c with
            | '\b' -> sb.Append("\\b") |> ignore
            | '\f' -> sb.Append("\\f") |> ignore
            | '\n' -> sb.Append("\\n") |> ignore
            | '\r' -> sb.Append("\\r") |> ignore
            | '\t' -> sb.Append("\\t") |> ignore
            | '"'  -> sb.Append("\\\"") |> ignore
            | '\\' -> sb.Append("\\\\") |> ignore
            | _ when Char.IsControl(c) -> 
                sb.AppendFormat("\\u{0:x4}", int c) |> ignore
            | _ -> sb.Append(c) |> ignore
        sb.Append('"') |> ignore
        sb.ToString()

    // RFC 8785 §3.2.3 requires sorting by UTF-16 code units.
    // In .NET, String.CompareOrdinal performs a UTF-16 code unit comparison.
    let private sortKeysExplicit (props: (string * CanonicalJson) list) =
        let arr = props |> List.toArray
        Array.sortInPlaceWith (fun (k1, _) (k2, _) -> String.CompareOrdinal(k1, k2)) arr
        arr |> Array.toList

    let rec serialize (json: CanonicalJson) =
        match json with
        | JNull -> "null"
        | JBool true -> "true"
        | JBool false -> "false"
        | JNumber n -> n.ToString(CultureInfo.InvariantCulture)
        | JString s -> escapeString s
        | JArray items ->
            let inner = items |> List.map serialize |> String.concat ","
            "[" + inner + "]"
        | JObject props ->
            let sortedProps = sortKeysExplicit props
            let inner = 
                sortedProps 
                |> List.map (fun (k, v) -> (escapeString k) + ":" + (serialize v))
                |> String.concat ","
            "{" + inner + "}"

    let encodeSubject (s: SubjectRecord) =
        JObject [
            "root", JString s.Root
            "schema", JString s.Schema
            "sourceDirectories", JArray (s.SourceDirectories |> List.map JString)
            "manifestDigest", (s.ManifestDigest |> Option.map JString |> Option.defaultValue JNull)
            "artifacts", JArray (
                s.Artifacts
                |> List.map (fun artifact ->
                    JObject [
                        "path", JString artifact.Path
                        "digest", JString artifact.Digest
                    ]))
        ]

    let encodeEvaluator (e: EvaluatorRecord) =
        JObject [
            "engineId", JString e.EngineId
            "engineVersion", JString e.EngineVersion
        ]

    let encodeContext (c: ReceiptContext) =
        JObject [
            "instant", JString c.Instant
            "timeProvenance", JString c.TimeProvenance
            "locale", JString c.Locale
            "networkPolicy", JString c.NetworkPolicy
        ]

    let encodeEvidenceRef (e: EvidenceRef) =
        JObject [
            "path", JString e.Path
            "kind", JString e.Kind
            "value", (match e.Value with | Some v -> JString v | None -> JNull)
            "provenance", (match e.Provenance with | Some p -> JString p | None -> JNull)
        ]

    let encodeAssessment (a: ComponentAssessmentRecord) =
        JObject [
            "componentId", JString a.ComponentId
            "componentVersion", JString a.ComponentVersion
            "health", JString (ReceiptText.health a.Health)
            "compliance", JString (ReceiptText.compliance a.Compliance)
            "applicableRules", JNumber a.ApplicableRules
            "evaluatedRules", JNumber a.EvaluatedRules
            "evidence", JArray (a.Evidence |> List.map encodeEvidenceRef)
        ]

    let encodeConstructiveEvidence (e: ConstructiveEvidenceReference) =
        JObject [
            "digest", JString e.Digest
            "kind", JString e.Kind
            "path", JString e.Path
            "provenance", (e.Provenance |> Option.map JString |> Option.defaultValue JNull)
        ]

    let encodeConstructiveGate (gate: ConstructiveGateAssessmentRecord) =
        JObject [
            "evidence",
                gate.Evidence
                |> List.map encodeConstructiveEvidence
                |> JArray
            "gateId", JString gate.GateId
            "gateVersion", JString gate.GateVersion
            "implementationDigest", JString gate.ImplementationDigest
            "verdict", JString (ReceiptText.verdict gate.Verdict)
        ]

    let encodeConstructiveAssessment (assessment: ConstructiveAssessmentRecord) =
        JObject [
            "derivationKind", JString assessment.DerivationKind
            "derivationReference",
                assessment.DerivationReference
                |> Option.map JString
                |> Option.defaultValue JNull
            "evaluatedGates", JNumber assessment.EvaluatedGates
            "gates",
                assessment.Gates
                |> List.map encodeConstructiveGate
                |> JArray
            "manifestDigest", JString assessment.ManifestDigest
            "missingGateIds",
                assessment.MissingGateIds
                |> List.map JString
                |> JArray
            "obligationId", JString assessment.ObligationId
            "projectionState", JString assessment.ProjectionState
            "requiredGates", JNumber assessment.RequiredGates
            "sourceDigest", JString assessment.SourceDigest
            "verdict", JString (ReceiptText.verdict assessment.Verdict)
        ]

    let encodeReceipt (r: CanonFlowEvidenceReceiptV11) =
        JObject [
            "schemaVersion", JString r.SchemaVersion
            "receiptType", JString r.ReceiptType
            "replayIdentity", JString r.ReplayIdentity
            "subject", encodeSubject r.Subject
            "evaluator", encodeEvaluator r.Evaluator
            "context", encodeContext r.Context
            "assessments", JArray (r.Assessments |> List.map encodeAssessment)
            "constructiveAssessments",
                JArray (
                    r.ConstructiveAssessments
                    |> List.map encodeConstructiveAssessment)
            "verdict", JString (ReceiptText.verdict r.Verdict)
        ]

    let serializeReceipt (env: CanonFlowEvidenceReceiptV11) =
        serialize (encodeReceipt env)

    let private encodeSeal seal =
        JObject [
            "status", JString (match seal.Status with | SealStatus.Signed -> "Signed" | SealStatus.Unsigned -> "Unsigned")
            "algorithm", (match seal.Algorithm with | Some SealAlgorithm.Ed25519 -> JString "Ed25519" | None -> JNull)
            "keyId", (match seal.KeyId with | Some value -> JString value | None -> JNull)
            "signature", (match seal.Signature with | Some value -> JString value | None -> JNull)
        ]

    let encodeEnvelope receipt =
        match encodeReceipt receipt with
        | JObject properties ->
            JObject (("seal", receipt.Seal |> Option.map encodeSeal |> Option.defaultValue JNull) :: properties)
        | _ -> invalidOp "Receipt encoding must produce an object."

    let serializeEnvelope receipt =
        receipt |> encodeEnvelope |> serialize

    let serializeSigningPayload receipt =
        let sealWithoutSignature =
            receipt.Seal
            |> Option.map (fun seal -> { seal with Signature = None })
        { receipt with Seal = sealWithoutSignature }
        |> serializeEnvelope

