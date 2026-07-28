namespace CanonFlow.Evaluator

open System
open System.Text.Json
open CanonFlow.Assurance

type SubjectManifest = {
    Root: string
    Artifacts: string list
}

type ConfigurationManifest = {
    OndcProfileRef: string option
    FsassayRulePack: string option
    ObligationManifestPath: string option
    ConstructiveEvidencePath: string option
    SealKeyPath: string option
    SealKeyId: string option
}

type EvaluationContextManifest = {
    Instant: string
    TimeProvenance: string
    Network: string
    Locale: string
}

type EvaluationManifest = {
    Schema: string
    Subject: SubjectManifest
    Profiles: string list
    Configuration: ConfigurationManifest option
    EvaluationContext: EvaluationContextManifest
    Budget: EvaluationBudget option
}

module ManifestParser =
    open Thoth.Json.Net
    open FsToolkit.ErrorHandling

    let private subjectDecoder =
        Decode.object (fun get ->
            { Root = get.Required.Field "root" Decode.string
              Artifacts = get.Optional.Field "artifacts" (Decode.list Decode.string) |> Option.defaultValue [] }
        )

    let private configDecoder =
        Decode.object (fun get ->
            { OndcProfileRef = get.Optional.Field "ondcProfileRef" Decode.string
              FsassayRulePack = get.Optional.Field "fsassayRulePack" Decode.string
              ObligationManifestPath = get.Optional.Field "obligationManifestPath" Decode.string
              ConstructiveEvidencePath = get.Optional.Field "constructiveEvidencePath" Decode.string
              SealKeyPath = get.Optional.Field "sealKeyPath" Decode.string
              SealKeyId = get.Optional.Field "sealKeyId" Decode.string }
        )

    let private contextDecoder =
        Decode.object (fun get ->
            { Instant = get.Required.Field "instant" Decode.string
              TimeProvenance = get.Required.Field "timeProvenance" Decode.string
              Network = get.Required.Field "network" Decode.string
              Locale = get.Required.Field "locale" Decode.string })

    let private budgetDecoder =
        Decode.object (fun get ->
            {
                MaxFiles = get.Required.Field "maxFiles" Decode.int
                MaxInputBytes = get.Required.Field "maxInputBytes" Decode.int64
                MaxJsonDepth = get.Required.Field "maxJsonDepth" Decode.int
                ComponentTimeoutSeconds = get.Required.Field "componentTimeoutSeconds" Decode.int
                TotalTimeoutSeconds = get.Required.Field "totalTimeoutSeconds" Decode.int
            })

    let private manifestDecoder =
        Decode.object (fun get ->
            { Schema = get.Optional.Field "$schema" Decode.string |> Option.defaultValue ""
              Subject = get.Required.Field "subject" subjectDecoder
              Profiles = get.Required.Field "profiles" (Decode.list Decode.string)
              Configuration = get.Optional.Field "configuration" configDecoder
              EvaluationContext = get.Required.Field "evaluationContext" contextDecoder
              Budget = get.Optional.Field "budget" budgetDecoder }
        )

    let private exactObject label allowed (element: JsonElement) =
        if element.ValueKind <> JsonValueKind.Object then
            Error $"{label} must be an object."
        else
            let properties = element.EnumerateObject() |> Seq.toList
            let names = properties |> List.map (fun property -> property.Name)
            let duplicates = names.Length <> (names |> Set.ofList |> Set.count)
            let unexpected =
                names
                |> List.filter (fun name -> not (allowed |> List.contains name))
                |> List.distinct
            if duplicates then Error $"{label} contains duplicate fields."
            elif not unexpected.IsEmpty then
                let fields = String.concat "," unexpected
                Error $"{label} contains unknown fields: {fields}."
            else Ok ()

    let private validateStructure (jsonString: string) =
        try
            use document = JsonDocument.Parse(jsonString, JsonDocumentOptions(MaxDepth = 128))
            let root = document.RootElement
            result {
                do! exactObject "manifest" ["$schema"; "subject"; "profiles"; "configuration"; "evaluationContext"; "budget"] root
                do! exactObject "subject" ["root"; "artifacts"] (root.GetProperty("subject"))
                do! exactObject "evaluationContext" ["instant"; "timeProvenance"; "network"; "locale"] (root.GetProperty("evaluationContext"))
                let hasConfiguration, configuration = root.TryGetProperty("configuration")
                if hasConfiguration then
                    do!
                        exactObject
                            "configuration"
                            [
                                "ondcProfileRef"
                                "fsassayRulePack"
                                "obligationManifestPath"
                                "constructiveEvidencePath"
                                "sealKeyPath"
                                "sealKeyId"
                            ]
                            configuration
                let hasBudget, budget = root.TryGetProperty("budget")
                if hasBudget then
                    do!
                        exactObject
                            "budget"
                            ["maxFiles"; "maxInputBytes"; "maxJsonDepth"; "componentTimeoutSeconds"; "totalTimeoutSeconds"]
                            budget
            }
        with ex ->
            Error $"Manifest JSON validation failed: {ex.Message}"

    let parse jsonString =
        validateStructure jsonString
        |> Result.bind (fun () -> Decode.fromString manifestDecoder jsonString)
        |> Result.bind (fun manifest ->
            if manifest.Profiles.IsEmpty then Error "At least one exact profile is required."
            elif manifest.EvaluationContext.Network <> "Forbidden" then Error "Normal assessment requires network=Forbidden."
            elif manifest.EvaluationContext.Locale <> "invariant" then Error "Assessment locale must be invariant."
            else
                match DateTimeOffset.TryParse(manifest.EvaluationContext.Instant, Globalization.CultureInfo.InvariantCulture) with
                | false, _ -> Error "evaluationContext.instant is invalid."
                | true, _ ->
                    match manifest.Budget with
                    | Some budget
                        when budget.MaxFiles <= 0
                             || budget.MaxInputBytes <= 0L
                             || budget.MaxJsonDepth <= 0
                             || budget.ComponentTimeoutSeconds <= 0
                             || budget.TotalTimeoutSeconds < budget.ComponentTimeoutSeconds ->
                        Error "Evaluation budget values are invalid."
                    | _ -> Ok manifest)

