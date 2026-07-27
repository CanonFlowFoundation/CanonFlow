namespace CanonFlow.Evaluator

open System

type SubjectManifest = {
    Root: string
    Artifacts: string list
}

type ConfigurationManifest = {
    OndcProfileRef: string option
    FsassayRulePack: string option
}

type EvaluationManifest = {
    Schema: string
    Subject: SubjectManifest
    Profiles: string list
    Configuration: ConfigurationManifest option
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
              FsassayRulePack = get.Optional.Field "fsassayRulePack" Decode.string }
        )

    let private manifestDecoder =
        Decode.object (fun get ->
            { Schema = get.Optional.Field "$schema" Decode.string |> Option.defaultValue ""
              Subject = get.Required.Field "subject" subjectDecoder
              Profiles = get.Required.Field "profiles" (Decode.list Decode.string)
              Configuration = get.Optional.Field "configuration" configDecoder }
        )

    let parse jsonString =
        Decode.fromString manifestDecoder jsonString

