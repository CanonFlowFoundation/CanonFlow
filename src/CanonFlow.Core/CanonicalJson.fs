namespace CanonFlow.Core.Verification

open System
open System.Text

type CanonicalJson =
    | JString of string
    | JBool   of bool
    | JNull
    | JArray  of CanonicalJson list
    | JObject of (string * CanonicalJson) list

module CanonicalJson =
    
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

    // Encoder functions to migrate from Thoth
    let encodeRuleOutcome (outcome: RuleOutcome) =
        match outcome with
        | Pass -> JString "Pass"
        | PassWithAssumptions -> JString "PassWithAssumptions"
        | Warning -> JString "Warning"
        | Unknown -> JString "Unknown"
        | NotSupported -> JString "NotSupported"
        | Fail -> JString "Fail"

    let encodeEvidenceKind (kind: EvidenceKind) =
        match kind with
        | Observed -> JString "Observed"
        | Parsed -> JString "Parsed"
        | UserConfirmed -> JString "UserConfirmed"
        | Derived -> JString "Derived"
        | Assumed -> JString "Assumed"
        | ExternalReference -> JString "ExternalReference"

    let encodeEvidence (e: Evidence) =
        JObject [
            "Path", JString e.Path
            "Kind", encodeEvidenceKind e.Kind
            "Value", (match e.Value with | Some v -> JString v | None -> JNull)
            "Provenance", (match e.Provenance with | Some p -> JString p | None -> JNull)
        ]

    let encodeRuleConfidence (c: RuleConfidence) =
        match c with
        | Exact -> JString "Exact"
        | Approximate -> JString "Approximate"
        | Advisory -> JString "Advisory"

    let encodeRuleMetadata (m: RuleMetadata) =
        JObject [
            "RuleId", JString m.RuleId
            "Category", JString m.Category
            "EffectiveFrom", (match m.EffectiveFrom with | Some d -> JString d | None -> JNull)
            "EffectiveUntil", (match m.EffectiveUntil with | Some d -> JString d | None -> JNull)
            "Reference", (match m.Reference with | Some r -> JString r | None -> JNull)
            "Confidence", encodeRuleConfidence m.Confidence
            "MessageKey", JString m.MessageKey
        ]

    let encodeRuleResult (r: RuleResult) =
        JObject [
            "Metadata", encodeRuleMetadata r.Metadata
            "Outcome", encodeRuleOutcome r.Outcome
            "Evidence", JArray (r.Evidence |> List.map encodeEvidence)
            "Parameters", JObject (r.Parameters |> Map.toList |> List.map (fun (k, v) -> k, JString v))
        ]

    let encodeVerdictEnvelope (env: VerdictEnvelope) =
        JObject [
            "SchemaVersion", JString env.SchemaVersion
            "EngineId", JString env.EngineId
            "EngineVersion", JString env.EngineVersion
            "RuleSetId", JString env.RuleSetId
            "RuleSetVersion", JString env.RuleSetVersion
            "SubjectType", JString env.SubjectType
            "SubjectHash", JString env.SubjectHash
            "OverallOutcome", encodeRuleOutcome env.OverallOutcome
            "Results", JArray (env.Results |> List.map encodeRuleResult)
        ]

    let serializeEnvelope (env: VerdictEnvelope) =
        serialize (encodeVerdictEnvelope env)
