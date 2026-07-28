namespace Canon.Cli

open System
open System.IO
open System.Text
open System.Text.Json
open Argu
open CanonFlow.Assurance
open CanonFlow.Assurance.Verification

type OndcEvaluateSdkArguments =
    | [<CliPrefix(CliPrefix.DoubleDash)>] Input of string
    | [<CliPrefix(CliPrefix.DoubleDash)>] Output of string
    | [<CliPrefix(CliPrefix.DoubleDash)>] Profile of string
    | [<CliPrefix(CliPrefix.DoubleDash)>] Instant of string

    interface IArgParserTemplate with // FsAssay-Ignore (Required by Argu framework)
        member value.Usage =
            match value with
            | Input _ -> "Evidence-bundle JSON path, or - for stdin."
            | Output _ -> "SDK result JSON path, or - for stdout."
            | Profile _ -> "Exact installed ONDC profile identifier."
            | Instant _ -> "Explicit RFC 3339 evaluation instant."

type ReceiptVerifySdkArguments =
    | [<CliPrefix(CliPrefix.DoubleDash)>] Receipt of string
    | [<CliPrefix(CliPrefix.DoubleDash)>] Public_Key_Hex of string
    | [<CliPrefix(CliPrefix.DoubleDash)>] Allow_Unsigned

    interface IArgParserTemplate with // FsAssay-Ignore (Required by Argu framework)
        member value.Usage =
            match value with
            | Receipt _ -> "Canonical receipt path, or - for stdin."
            | Public_Key_Hex _ -> "Hex-encoded Ed25519 public key."
            | Allow_Unsigned -> "Verify canonical integrity without requiring a signature."

module SdkProtocol =
    [<Literal>]
    let ProtocolVersion = "1.0"

    [<Literal>]
    let PreviewProfile = "ondc-retail-1.2.0-preview"

    let private maxInputBytes = 16 * 1024 * 1024
    let private jsonOptions = JsonSerializerOptions(WriteIndented = false)
    let private strictUtf8 = UTF8Encoding(false, true)

    let private readBoundedStream (stream: Stream) =
        use output = new MemoryStream()
        let buffer = Array.zeroCreate<byte> 81920
        let rec copy total =
            let count = stream.Read(buffer, 0, buffer.Length)
            if count = 0 then
                Ok (strictUtf8.GetString(output.ToArray()))
            elif total + count > maxInputBytes then
                Error "Input exceeds the 16 MiB SDK protocol budget."
            else
                output.Write(buffer, 0, count)
                copy (total + count)
        copy 0

    let private readInput (path: string) =
        try
            if path = "-" then
                Console.OpenStandardInput() |> readBoundedStream
            else
                let fullPath = Path.GetFullPath(path)
                let info = FileInfo(fullPath)
                if info.Length > int64 maxInputBytes then
                    Error "Input exceeds the 16 MiB SDK protocol budget."
                else
                    use stream = File.OpenRead(fullPath)
                    readBoundedStream stream
        with ex ->
            Error $"Cannot read input: {ex.Message}"

    let private writeOutput (path: string) (content: string) =
        try
            if path = "-" then
                Console.Out.WriteLine(content)
            else
                let fullPath = Path.GetFullPath(path)
                let parent = Path.GetDirectoryName(fullPath)
                if not (String.IsNullOrWhiteSpace(parent)) then
                    Directory.CreateDirectory(parent) |> ignore
                File.WriteAllText(fullPath, content, UTF8Encoding(false))
            Ok ()
        with ex ->
            Error $"Cannot write output: {ex.Message}"

    let private serialize value =
        JsonSerializer.Serialize(value, jsonOptions)

    let private errorPayload code message =
        serialize
            {|
                schemaVersion = ProtocolVersion
                error =
                    {|
                        code = code
                        message = message
                    |}
            |}

    let private emitError output code message =
        let payload = errorPayload code message
        match writeOutput output payload with
        | Ok () -> 64
        | Error writeError ->
            Console.Error.WriteLine(writeError)
            74

    let private manifestJson profile instant =
        serialize
            {|
                ``$schema`` = "https://canonflow.dev/schemas/evaluation-manifest-v1.json"
                subject =
                    {|
                        root = "."
                        artifacts = [| "evidence-bundle.json" |]
                    |}
                evaluationContext =
                    {|
                        instant = instant
                        timeProvenance = "Declared"
                        network = "Forbidden"
                        locale = "invariant"
                    |}
                profiles = [| profile |]
            |}

    let private evaluate
        (input: string)
        (output: string)
        (profile: string)
        (instant: string)
        =
        let validInstant =
            instant.EndsWith("Z", StringComparison.Ordinal)
            && match DateTimeOffset.TryParse(
                         instant,
                         Globalization.CultureInfo.InvariantCulture,
                         Globalization.DateTimeStyles.RoundtripKind) with
               | true, value -> value.Offset = TimeSpan.Zero
               | _ -> false
        if not validInstant then
            emitError
                output
                "INVALID_INSTANT"
                "instant must be an explicit UTC RFC 3339 timestamp ending in Z."
        elif profile <> PreviewProfile then
            emitError
                output
                "PROFILE_NOT_INSTALLED"
                $"Profile '{profile}' is not installed. Use capabilities --json to list exact admitted profiles."
        else
            match readInput input with
            | Error error -> emitError output "INPUT_READ_FAILED" error
            | Ok bundle ->
                let inputBytes = Encoding.UTF8.GetByteCount(bundle)
                if inputBytes = 0 then
                    emitError output "EMPTY_INPUT" "The ONDC evidence bundle is empty."
                elif inputBytes > maxInputBytes then
                    emitError output "INPUT_BUDGET_EXCEEDED" "The ONDC evidence bundle exceeds the 16 MiB SDK input budget."
                else
                    let tempRoot =
                        Path.Combine(Path.GetTempPath(), "canonflow-ondc-sdk-" + Guid.NewGuid().ToString("N"))
                    try
                        try
                            Directory.CreateDirectory(tempRoot) |> ignore
                            let bundlePath = Path.Combine(tempRoot, "evidence-bundle.json")
                            let manifestPath = Path.Combine(tempRoot, "canonflow-evaluation.json")
                            File.WriteAllText(bundlePath, bundle, UTF8Encoding(false))
                            File.WriteAllText(manifestPath, manifestJson profile instant, UTF8Encoding(false))

                            match CanonFlow.Evaluator.Pipeline.evaluate manifestPath with
                            | Error error -> emitError output "EVALUATION_REJECTED" error
                            | Ok run ->
                                use verdictDocument =
                                    JsonDocument.Parse(CanonFlow.Reports.VerdictView.generate run.Receipt)
                                let result = verdictDocument.RootElement.Clone()
                                let payload =
                                    serialize
                                        {|
                                            schemaVersion = ProtocolVersion
                                            profile = profile
                                            result = result
                                            receipt = run.CanonicalReceipt
                                        |}
                                match writeOutput output payload with
                                | Ok () -> run.ExitCode
                                | Error error ->
                                    Console.Error.WriteLine(error)
                                    74
                        with ex ->
                            emitError output "SDK_PROTOCOL_FAILURE" ex.Message
                    finally
                        if Directory.Exists(tempRoot) then
                            Directory.Delete(tempRoot, true)

    let runOndcEvaluate argv =
        let parser =
            ArgumentParser.Create<OndcEvaluateSdkArguments>(
                programName = "canonflow ondc evaluate",
                errorHandler = ProcessExiter())
        let args = parser.ParseCommandLine(inputs = argv, raiseOnUsage = true)
        let input = args.GetResult(Input)
        let output = args.GetResult(Output)
        let profile = args.TryGetResult(Profile) |> Option.defaultValue PreviewProfile
        let instant = args.GetResult(Instant)
        evaluate input output profile instant

    let runReceiptVerify argv =
        let parser =
            ArgumentParser.Create<ReceiptVerifySdkArguments>(
                programName = "canonflow receipt verify",
                errorHandler = ProcessExiter())
        let args = parser.ParseCommandLine(inputs = argv, raiseOnUsage = true)
        let receiptPath = args.GetResult(Receipt)
        match readInput receiptPath with
        | Error error ->
            Console.Out.WriteLine(
                serialize
                    {|
                        schemaVersion = ProtocolVersion
                        valid = false
                        digest = null
                        error = error
                    |})
            3
        | Ok receipt ->
            try
                let publicKey =
                    args.TryGetResult(Public_Key_Hex)
                    |> Option.map Convert.FromHexString
                let allowUnsigned = args.Contains(Allow_Unsigned)
                match ReceiptVerifier.verifyEnvelopeJson receipt publicKey allowUnsigned with
                | Ok digest ->
                    Console.Out.WriteLine(
                        serialize
                            {|
                                schemaVersion = ProtocolVersion
                                valid = true
                                digest = digest
                                error = null
                            |})
                    0
                | Error error ->
                    Console.Out.WriteLine(
                        serialize
                            {|
                                schemaVersion = ProtocolVersion
                                valid = false
                                digest = null
                                error = error
                            |})
                    1
            with ex ->
                Console.Out.WriteLine(
                    serialize
                        {|
                            schemaVersion = ProtocolVersion
                            valid = false
                            digest = null
                            error = ex.Message
                        |})
                3

    let runCapabilities json =
        if json then
            Console.Out.WriteLine(
                serialize
                    {|
                        schemaVersion = ProtocolVersion
                        evaluator =
                            {|
                                id = "CanonFlow.Evaluator"
                                version = "0.1.0-alpha"
                            |}
                        sdkProtocol =
                            {|
                                version = ProtocolVersion
                                commands = [| "ondc evaluate"; "receipt verify"; "capabilities" |]
                            |}
                        claims =
                            {|
                                vocabulary =
                                    [|
                                        "Verified"
                                        "ConstructivelyProjected"
                                        "Inconclusive"
                                        "Unsupported"
                                        "Experimental"
                                    |]
                                verifiedMeans = "admitted-rules-and-evidence-only"
                            |}
                        constructiveModelling =
                            {|
                                status = "dormant"
                                productionEmission = false
                                laboratoryProfiles =
                                    [|
                                        {|
                                            id = "required-contact-postgres-v1-lab"
                                            status = "experimental"
                                            sourceDigest = "sha256:8a71fd4510146dbd2bf2822eef5b7934bfef70612b3fa1ad97d69d5938c2bded"
                                            projectionState = "Admitted"
                                            derivation =
                                                {|
                                                    kind = "Admitted"
                                                    admissionId = "cff:admission:cm2-required-contact-lab"
                                                |}
                                            obligationId = "cff:lab:required-contact"
                                            receiptProfile = "required-contact-constructive-v1"
                                            productionEmission = false
                                        |}
                                    |]
                            |}
                        obligationManifest =
                            {|
                                manifestType = ObligationManifest.ManifestType
                                schemaVersion = ObligationManifest.SchemaVersion
                            |}
                        profiles =
                            [|
                                {|
                                    id = PreviewProfile
                                    protocol = "ONDC"
                                    protocolVersion = "1.2.0"
                                    status = "experimental-preview"
                                    authority = "none"
                                    applicableRules = 10
                                |}
                            |]
                        receipt =
                            {|
                                schemaVersion = "1.1"
                                typeName = "CanonFlowEvidenceReceipt"
                                verificationField = "assessments"
                                constructiveField = "constructiveAssessments"
                            |}
                    |})
        else
            Console.Out.WriteLine("CanonFlow SDK protocol 1.0")
            Console.Out.WriteLine("Constructive modelling: dormant (production emission disabled)")
            Console.Out.WriteLine($"Installed profile: {PreviewProfile} (experimental; not ONDC certification)")
        0
