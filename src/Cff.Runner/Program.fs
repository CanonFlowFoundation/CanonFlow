namespace Cff.Runner

open System
open System.IO
open System.Text
open System.Text.Json
open Cff.Harness
open CanonFlow.Assurance.Contracts

module Program =
    let private printReport report =
        printfn "CFF demo: %s" report.Name
        for check in report.Checks do
            let marker = if check.Passed then "✓" else "✗"
            printfn "%s %s" marker check.Name
            printfn "  %s" check.Detail
        if report.Succeeded then
            printfn "PASS — every protected check completed"
            0
        else
            eprintfn "BLOCKED — one or more protected checks did not complete"
            2

    let private sourceRoot arguments =
        arguments
        |> Array.tryFindIndex ((=) "--source-root")
        |> Option.bind (fun index ->
            if index + 1 < arguments.Length then Some arguments.[index + 1] else None)
        |> Option.orElseWith (fun () ->
            Environment.GetEnvironmentVariable("CFF_ONDC_SOURCE_ROOT")
            |> Option.ofObj
            |> Option.filter (String.IsNullOrWhiteSpace >> not))
        |> Option.defaultValue "/tmp/ondc-ret-1.2.5"

    let private sourceVerify path =
        let checks = Demo.verifyOfficialSource path
        for check in checks do
            printfn "%s %s — %s" (if check.Passed then "✓" else "✗") check.Name check.Detail
        if checks |> List.forall _.Passed then 0 else 2

    let private requiredString (name: string) (root: JsonElement) =
        root.EnumerateObject()
        |> Seq.tryFind (fun item -> item.NameEquals name)
        |> Option.map _.Value
        |> function
            | Some value
                when value.ValueKind = JsonValueKind.String
                     && not (String.IsNullOrWhiteSpace(value.GetString())) ->
                Ok (value.GetString())
            | _ -> Error ("Missing non-blank string property: " + name)

    let private ruleValidate path =
        if not (File.Exists path) then
            eprintfn "Rule file not found: %s" path
            2
        else
            let bytes = File.ReadAllBytes path
            match Canonical.canonicalizeJson bytes with
            | Error error ->
                eprintfn "Rule rejected: %s" error
                2
            | Ok canonical ->
                try
                    use document = JsonDocument.Parse(canonical)
                    let required =
                        [ "ruleId"; "title"; "evaluatorId"; "authority"; "applicability"; "requiredEvidence" ]
                    let errors =
                        required
                        |> List.choose (fun name ->
                            document.RootElement.EnumerateObject()
                            |> Seq.exists (fun item -> item.NameEquals name)
                            |> function
                                | true -> None
                                | false -> Some ("Missing property: " + name))
                    match requiredString "ruleId" document.RootElement, errors with
                    | Ok ruleId, [] ->
                        printfn "✓ rule JSON is canonicalizable and structurally complete"
                        printfn "  ruleId: %s" ruleId
                        printfn "  digest: %s" (canonical |> Canonical.sha256Bytes |> ContentDigest.value)
                        0
                    | Error error, _ ->
                        eprintfn "Rule rejected: %s" error
                        2
                    | _, values ->
                        eprintfn "Rule rejected: %s" (String.concat "; " values)
                        2
                with :? JsonException as error ->
                    eprintfn "Rule rejected: %s" error.Message
                    2

    let private rulePackBuild directory =
        if not (Directory.Exists directory) then
            eprintfn "Rule-pack directory not found: %s" directory
            2
        else
            let rules = Directory.GetFiles(directory, "*.rule.json") |> Array.sort
            if rules.Length = 0 then
                eprintfn "Rule pack rejected: an empty rule pack cannot pass"
                2
            else
                let canonical =
                    rules
                    |> Array.map (fun path ->
                        match Canonical.canonicalizeJson (File.ReadAllBytes path) with
                        | Ok bytes -> Path.GetFileName(path), bytes
                        | Error error -> invalidOp (path + ": " + error))
                let manifest =
                    canonical
                    |> Array.map (fun (name, bytes) ->
                        name + " " + (bytes |> Canonical.sha256Bytes |> ContentDigest.value))
                    |> String.concat "\n"
                let manifestBytes = Encoding.UTF8.GetBytes(manifest + "\n")
                let digest = Canonical.sha256Bytes manifestBytes
                let contentAddressed =
                    Path.Combine(directory, ContentDigest.value digest |> fun value -> value.Replace(":", "-") + ".rulepack")
                File.WriteAllBytes(contentAddressed, manifestBytes)
                printfn "✓ non-empty rule pack built"
                printfn "  %s" (ContentDigest.value digest)
                printfn "  %s" contentAddressed
                0

    let private workStatePath directory = Path.Combine(directory, "work.state")

    let private readStage directory =
        let path = workStatePath directory
        if File.Exists path then File.ReadAllText(path).Trim() |> Some else None

    let private unavailableWorkTransition operation directory =
        eprintfn "BLOCKED: '%s' is not operational for %s." operation directory
        eprintfn "A typed admitted bundle, authorized principal, independently captured observation, and signed artifact are required."
        eprintfn "The runner will not promote a plain stage marker into evidence."
        2

    let private workInit workId =
        if String.IsNullOrWhiteSpace workId then
            eprintfn "Work ID cannot be blank"
            2
        else
            let directory = Path.GetFullPath(Path.Combine("work", workId))
            if Directory.Exists directory then
                eprintfn "Work already exists: %s" directory
                2
            else
                Directory.CreateDirectory(directory) |> ignore
                File.WriteAllText(workStatePath directory, "Proposed\n", UTF8Encoding(false))
                File.WriteAllText(Path.Combine(directory, "work.id"), workId + "\n", UTF8Encoding(false))
                printfn "✓ proposed work initialized: %s" directory
                0

    let private usage () =
        eprintfn "Usage:"
        eprintfn "  cff demo ondc-quote-continuity [--source-root PATH]"
        eprintfn "  cff demo ondc-beckn24"
        eprintfn "  cff source verify PATH"
        eprintfn "  cff rule validate FILE"
        eprintfn "  cff rulepack build DIRECTORY"
        eprintfn "  cff work init WORK-ID"
        eprintfn "  cff work validate|admit|witness-red|register-change|assess|review|seal DIRECTORY"
        eprintfn "Commands fail closed when artifacts, evidence, or supported workflow stages are absent."
        64

    [<EntryPoint>]
    let main arguments =
        try
            match arguments with
            | [| "demo"; "ondc-quote-continuity" |]
            | [| "demo"; "ondc-quote-continuity"; "--source-root"; _ |] ->
                Demo.runQuoteContinuity (sourceRoot arguments) |> printReport
            | [| "demo"; "ondc-beckn24" |] -> Demo.runBeckn24 () |> printReport
            | [| "source"; "verify"; path |] -> sourceVerify path
            | [| "rule"; "validate"; path |] -> ruleValidate path
            | [| "rulepack"; "build"; directory |] -> rulePackBuild directory
            | [| "work"; "init"; workId |] -> workInit workId
            | [| "work"; "validate"; directory |] ->
                match readStage directory with
                | Some stage ->
                    eprintfn "BLOCKED: stage marker '%s' exists, but typed digest validation is not implemented." stage
                    2
                | None ->
                    eprintfn "Work state is absent"
                    2
            | [| "work"; "admit"; directory |] -> unavailableWorkTransition "work admit" directory
            | [| "work"; "witness-red"; directory |] -> unavailableWorkTransition "work witness-red" directory
            | [| "work"; "register-change"; directory |] -> unavailableWorkTransition "work register-change" directory
            | [| "work"; "assess"; directory |] -> unavailableWorkTransition "work assess" directory
            | [| "work"; "review"; directory |] -> unavailableWorkTransition "work review" directory
            | [| "work"; "seal"; directory |] -> unavailableWorkTransition "work seal" directory
            | _ -> usage ()
        with error ->
            eprintfn "CFF runner tool failure: %s" error.Message
            70
