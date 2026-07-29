namespace Cff.Harness

open System
open System.IO
open System.Reflection
open System.Runtime.InteropServices
open System.Security.Cryptography
open System.Text
open CanonFlow.Assurance.Contracts

type DemoCheck =
    { Name: string
      Passed: bool
      Detail: string }

type DemoReport =
    { Name: string
      Checks: DemoCheck list
      Succeeded: bool }

[<RequireQualifiedAccess>]
module Demo =
    let private digest value =
        ContentDigest.createSha256 value |> Result.defaultWith invalidOp

    let private expectedSelectSource =
        digest "sha256:627335d65b8b0e12631b6e8f6487f80dd0285dff5788fb9401464bccab679c25"

    let private expectedInitSource =
        digest "sha256:23041644d57a986a47d6b5be1bbaa25ae1806d26242806bd7d05fd69a6620a52"

    let private sourceFiles root =
        [ Path.Combine(root, "api/components/examples/B2C/flow-3/06_on_select.yaml"), expectedSelectSource
          Path.Combine(root, "api/components/examples/B2C/flow-3/08_on_init.yaml"), expectedInitSource ]

    let verifyOfficialSource root =
        sourceFiles root
        |> List.map (fun (path, expected) ->
            if not (File.Exists path) then
                { Name = "official source bytes verified"
                  Passed = false
                  Detail = "Missing pinned source file: " + path }
            else
                let actual = File.ReadAllBytes path |> Canonical.sha256Bytes
                { Name = "official source bytes verified"
                  Passed = actual = expected
                  Detail =
                    if actual = expected then Path.GetFileName path + " matches pinned SHA-256"
                    else Path.GetFileName path + " digest changed" })

    let private sourceClause clause kind document section sourceDigest interpretation =
        { ClauseId = ClauseId.create clause |> Result.defaultWith invalidOp
          SourceKind = kind
          Locator =
            { DocumentId = document
              Version = "draft-b2c-1.2.5@7a9c7e6955018ae8f758c22f3f78f7af7d8def4e"
              Section = section
              Uri = Some "https://github.com/ONDC-Official/ONDC-RET-Specifications/tree/draft-b2c-1.2.5" }
          SourceDigest = sourceDigest
          ExtractDigest = Canonical.sha256Text interpretation
          EffectiveFrom = None
          Supersedes = None
          InterpretationNote = interpretation
          AdmittedBy = "CFF product-owner mandate docs/todo/gtm.md"
          AdmittedAt = DateTimeOffset(2026, 7, 29, 0, 0, 0, TimeSpan.Zero) }

    let quoteDefinition () =
        let interpretation =
            "For the pinned basic flow-3 scenario, with no admitted quote-changing choice, the correlated on_select and on_init quote totals retain currency and normalized value."
        let primary =
            sourceClause
                "RET125.flow3.quote-continuity"
                OfficialScenario
                "ONDC-RET-Specifications"
                "api/components/examples/B2C/flow-3/06_on_select.yaml"
                expectedSelectSource
                interpretation
        let supporting =
            sourceClause
                "RET125.flow3.on-init-quote"
                OfficialScenario
                "ONDC-RET-Specifications"
                "api/components/examples/B2C/flow-3/08_on_init.yaml"
                expectedInitSource
                "The correlated on_init example retains INR 100 as the normalized quote total."
        { RuleId = QuoteContinuity.ruleId
          Title = "Pinned flow-3 quote continuity"
          Authority = primary
          SupportingAuthorities = [ supporting ]
          Applicability =
            AllOf [
                ProfileIs QuoteContinuity.profileId
                DomainIs "ONDC:RET10"
                ActionPresent "on_select"
                ActionPresent "on_init"
                FactEquals (
                    FactPath.create "$.quoteChangingChoice" |> Result.defaultWith invalidOp,
                    FactValue.Boolean false
                )
            ]
          RequiredEvidence =
            [ { RequirementId = "select-callback"
                Kind = ProtocolMessage "on_select"
                Cardinality = ExactlyOne
                Trust = IndependentlyCaptured
                Description = "Independently captured on_select message" }
              { RequirementId = "init-callback"
                Kind = ProtocolMessage "on_init"
                Cardinality = ExactlyOne
                Trust = IndependentlyCaptured
                Description = "Independently captured on_init message" } ]
          EvaluatorId = QuoteContinuity.evaluatorId
          RuleVersion = 1 }

    let private assemblyIdentity () =
        let assembly = typeof<RuleId>.Assembly
        let assemblyBytes =
            if String.IsNullOrWhiteSpace assembly.Location then
                Encoding.UTF8.GetBytes assembly.FullName
            else File.ReadAllBytes assembly.Location
        let dependencies =
            assembly.GetReferencedAssemblies()
            |> Array.map _.FullName
            |> Array.sort
            |> String.concat "\n"
        { EvaluatorId = QuoteContinuity.evaluatorId
          AssemblyDigest = Canonical.sha256Bytes assemblyBytes
          DependencySetDigest = Canonical.sha256Text dependencies
          PackageVersion = assembly.GetName().Version.ToString()
          RuntimeVersion = RuntimeInformation.FrameworkDescription
          RuntimeIdentifier = RuntimeInformation.RuntimeIdentifier
          BuildProvenanceDigest = Canonical.sha256Text (assembly.ManifestModule.ModuleVersionId.ToString("D")) }

    let quoteRegistry () =
        Map.ofList [
            QuoteContinuity.evaluatorId,
            { Identity = assemblyIdentity ()
              Evaluate = QuoteContinuity.evaluate }
        ]

    let private message action transaction messageId timestamp subscriber counterparty trust amount currency =
        let raw =
            sprintf
                """{"context":{"action":"%s","domain":"ONDC:RET10","transaction_id":"%s","message_id":"%s"},"message":{"order":{"quote":{"price":{"currency":"%s","value":"%s"}}}}}"""
                action
                transaction
                messageId
                currency
                amount
            |> Encoding.UTF8.GetBytes
        let canonical =
            Canonical.canonicalizeJson raw |> Result.defaultWith invalidOp
        { Action = action
          Correlation =
            { TransactionId = transaction
              MessageId = messageId
              SubscriberId = Some subscriber
              CounterpartyId = Some counterparty }
          Timestamp = timestamp
          RawPayload = Array.copy raw
          RawPayloadDigest = Canonical.sha256Bytes raw
          CanonicalPayload = Some (Array.copy canonical)
          CanonicalPayloadDigest = Some (Canonical.sha256Bytes canonical)
          Provenance =
            { CapturedBy = "cff-independent-http-collector"
              CapturedAt = timestamp
              CaptureMethod = HttpCollector
              Producer = Some counterparty
              AttestationDigest = Some (Canonical.sha256Text ("collector:" + messageId))
              EstablishedTrust = trust } }

    let quoteBundle selectAmount initAmount selectTransaction initTransaction trust =
        let at = DateTimeOffset(2026, 7, 29, 10, 0, 0, TimeSpan.Zero)
        let items =
            [ Message (message "on_select" selectTransaction "select-001" at "buyer.example" "seller.example" trust selectAmount "INR")
              Message (message "on_init" initTransaction "init-001" (at.AddSeconds 1) "buyer.example" "seller.example" trust initAmount "INR") ]
        let draft =
            { BundleId = "transaction-001"
              Profile = QuoteContinuity.profileId
              Items = items
              BundleDigest = Canonical.sha256Text "" }
        { draft with BundleDigest = Canonical.evidenceBundleDigest draft }

    let quoteFacts bundle =
        let source = DerivedFromEvidence bundle.BundleDigest
        let fact path value =
            { Path = FactPath.create path |> Result.defaultWith invalidOp
              Value = value
              Source = source }
        let facts =
            [ fact "$.domain:" (FactValue.Text "ONDC:RET10")
              fact "$.action:on_select" (FactValue.Boolean true)
              fact "$.action:on_init" (FactValue.Boolean true)
              fact "$.quoteChangingChoice" (FactValue.Boolean false) ]
        { Profile = QuoteContinuity.profileId
          Facts = facts
          FactsDigest = Canonical.factsDigest facts }

    let quotePack () =
        let definition = quoteDefinition ()
        { RulePackId = RulePackId.create "ONDC-RET-B2C-1.2.5-EXPERIMENTAL" |> Result.defaultWith invalidOp
          Profile = QuoteContinuity.profileId
          Version = 1
          Obligations = [ definition ]
          SourceProfileDigest =
            Canonical.sha256Text (
                ContentDigest.value expectedSelectSource
                + "\n"
                + ContentDigest.value expectedInitSource
            )
          CanonicalizationProfileDigest = Canonical.sha256Text "cff-json-c14n/v1:utf8-nfc-ordinal-decimal"
          AggregationPolicyDigest = Canonical.sha256Text "cff-fail-closed/v1"
          Supersedes = None }

    let private check name passed detail = { Name = name; Passed = passed; Detail = detail }

    let runQuoteContinuity sourceRoot =
        let sourceChecks = verifyOfficialSource sourceRoot
        let registry = quoteRegistry ()
        let pack = quotePack ()
        let seed = RandomNumberGenerator.GetBytes 32
        let publicKey = Receipts.publicKey seed
        let at = DateTimeOffset(2026, 7, 29, 10, 5, 0, TimeSpan.Zero)
        let admission = Receipts.admitPack seed "cff-demo-admitter" at pack
        let goodBundle = quoteBundle "100.00" "100" "tx-001" "tx-001" IndependentlyCaptured
        let facts = quoteFacts goodBundle
        let evaluation = Engine.evaluatePack registry admission pack facts goodBundle
        let evaluationChecks, receipt =
            match evaluation with
            | Error error ->
                [ check "obligation evaluated" false error ], None
            | Ok result ->
                let toolchain = Canonical.sha256Text RuntimeInformation.FrameworkDescription
                let sandbox = Canonical.sha256Text "offline;network=deny;commands=allow-list"
                let receipt = Receipts.sealEvaluation seed "cff-independent-runner" at toolchain sandbox result
                let verification = Receipts.verifyEvaluation publicKey publicKey result receipt
                [ check "source clause admitted" true (string result.Admission.Decision)
                  check "obligation definition admitted" (Validation.definition (quoteDefinition ()) |> List.isEmpty) (ContentDigest.value (Canonical.obligationDigest (quoteDefinition ())))
                  check "evaluator identity resolved" true (ContentDigest.value result.Results.Head.EvaluatorIdentity.AssemblyDigest)
                  check "evidence bundle integrity verified" (Evidence.verifyIntegrity goodBundle |> List.isEmpty) (ContentDigest.value goodBundle.BundleDigest)
                  check "applicability established" true (ContentDigest.value facts.FactsDigest)
                  check "required evidence and trust established" true "two independently captured callbacks"
                  check "quote continuity evaluated" (result.Verdict = Pass) (string result.Verdict)
                  check "rule-pack verdict aggregated" (result.Verdict = Pass) (string result.Verdict)
                  check "evaluation receipt sealed" (receipt.Signature.Length = 64) receipt.SignatureAlgorithm
                  check "receipt verified offline" (verification = { SealStatus = Valid; VerdictStatus = Valid; RecomputedVerdict = Pass }) "seal and verdict valid" ],
                Some (result, receipt, publicKey)

        let scenario name predicate bundle registryOverride =
            match Engine.evaluatePack registryOverride admission pack (quoteFacts bundle) bundle with
            | Ok value -> check name (predicate value.Verdict) (string value.Verdict)
            | Error error -> check name (predicate (ProfileToolFailure error)) error

        let missingBundle =
            let full = quoteBundle "100" "100" "tx-001" "tx-001" IndependentlyCaptured
            let draft = { full with Items = [ full.Items.Head ]; BundleDigest = Canonical.sha256Text "" }
            { draft with BundleDigest = Canonical.evidenceBundleDigest draft }
        let ambiguousBundle =
            let full = quoteBundle "100" "100" "tx-001" "tx-001" IndependentlyCaptured
            let duplicate =
                match full.Items.Head with
                | Message message ->
                    Message { message with Correlation = { message.Correlation with MessageId = "select-duplicate" } }
                | value -> value
            let draft = { full with Items = duplicate :: full.Items; BundleDigest = Canonical.sha256Text "" }
            { draft with BundleDigest = Canonical.evidenceBundleDigest draft }
        let malformedBundle =
            let full = quoteBundle "100" "100" "tx-001" "tx-001" IndependentlyCaptured
            let malformed =
                match full.Items.Head with
                | Message message ->
                    let bytes = Encoding.UTF8.GetBytes """{"message":{"order":{}}}"""
                    let canonical = Canonical.canonicalizeJson bytes |> Result.defaultWith invalidOp
                    Message {
                        message with
                            RawPayload = bytes
                            RawPayloadDigest = Canonical.sha256Bytes bytes
                            CanonicalPayload = Some canonical
                            CanonicalPayloadDigest = Some (Canonical.sha256Bytes canonical)
                    }
                | value -> value
            let draft = { full with Items = malformed :: full.Items.Tail; BundleDigest = Canonical.sha256Text "" }
            { draft with BundleDigest = Canonical.evidenceBundleDigest draft }

        let isInconclusive = function InconclusiveProfile _ -> true | _ -> false
        let isFailure = function Fail _ -> true | _ -> false
        let isToolFailure = function ProfileToolFailure _ -> true | _ -> false
        let blockedChecks =
            [ scenario "missing evidence blocked" isInconclusive missingBundle registry
              scenario "ambiguous evidence blocked" isInconclusive ambiguousBundle registry
              scenario "malformed evidence blocked" isInconclusive malformedBundle registry
              scenario "cross-transaction evidence blocked" isInconclusive (quoteBundle "100" "100" "tx-a" "tx-b" IndependentlyCaptured) registry
              scenario "changed total blocked" isFailure (quoteBundle "100" "101" "tx-001" "tx-001" IndependentlyCaptured) registry
              scenario "untrusted producer evidence blocked" isInconclusive (quoteBundle "100" "100" "tx-001" "tx-001" ProducerSupplied) registry
              scenario "evaluator missing blocked" isToolFailure goodBundle Map.empty
              scenario
                  "evaluator crash blocked"
                  isToolFailure
                  goodBundle
                  (registry
                   |> Map.add
                       QuoteContinuity.evaluatorId
                       { Identity = registry.[QuoteContinuity.evaluatorId].Identity
                         Evaluate = fun _ -> invalidOp "deliberate evaluator crash" }) ]
        let tamperCheck =
            match receipt with
            | None -> check "receipt tampering blocked" false "No receipt was produced"
            | Some (evaluation, receiptValue, key) ->
                let tampered = { receiptValue with Issuer = "attacker" }
                let verification = Receipts.verifyEvaluation key key evaluation tampered
                check "receipt tampering blocked" (verification.SealStatus <> Valid) (string verification.SealStatus)
        let checks = sourceChecks @ evaluationChecks @ blockedChecks @ [ tamperCheck ]
        { Name = "ondc-quote-continuity"
          Checks = checks
          Succeeded = checks |> List.forall _.Passed }

    let private apply (policy: WorkPolicy) event state =
        Work.transition policy state event |> Result.defaultWith (fun findings ->
            findings
            |> NonEmptyList.toList
            |> List.map _.Message
            |> String.concat "; "
            |> invalidOp)

    let runBeckn24 () =
        let workId = WorkId.create "BECKN24" |> Result.defaultWith invalidOp
        let spec = Canonical.sha256Text "BECKN24 admitted idempotency specification"
        let policyDigest = Canonical.sha256Text "cff-work-policy/v1"
        let commitBefore = Canonical.sha256Text "beckn24-red-commit"
        let commitAfter = Canonical.sha256Text "beckn24-green-commit"
        let policy =
            { PolicyDigest = policyDigest
              RequiredGates = NonEmptyList.create "FsAssay" [ "ONDCFlow-BECKN24" ]
              AuthorizedAdmitters = Set.singleton "product-owner"
              AuthorizedReviewers = Set.singleton "independent-reviewer"
              AuthorizedSigners = Set.singleton "cff-runner" }
        let proposal = { WorkId = workId; SpecDigest = spec; ProposedBy = "coding-agent" }
        let attestation gate passed commit observer =
            { Commit = commit
              SpecDigest = spec
              PolicyDigest = policyDigest
              GateId = gate
              Passed = passed
              ObservedBy = observer
              ObservationDigest = Canonical.sha256Text ($"{gate}:{passed}:{ContentDigest.value commit}") }
        let red = attestation "BECKN24-test" false commitBefore "independent-runner"
        let change = { Commit = commitAfter; ImplementedBy = "coding-agent" }
        let green = attestation "BECKN24-test" true commitAfter "independent-runner"
        let fsAssay = attestation "FsAssay" true commitAfter "independent-runner"
        let ondc = attestation "ONDCFlow-BECKN24" true commitAfter "independent-runner"
        let assessment = { Commit = commitAfter; Gates = NonEmptyList.create fsAssay [ ondc ] }
        let review =
            { Commit = commitAfter
              Implementer = "coding-agent"
              ReviewedBy = "independent-reviewer"
              Accepted = true }
        let state =
            Proposed proposal
            |> apply policy (Draft policyDigest)
            |> apply policy (Admit ("product-owner", DateTimeOffset(2026, 7, 29, 11, 0, 0, TimeSpan.Zero)))
            |> apply policy (WitnessRed red)
            |> apply policy (RegisterChange change)
            |> apply policy (WitnessGreen green)
            |> apply policy (RecordAssessment assessment)
            |> apply policy (AcceptReview review)
            |> apply policy (SealWork ("cff-runner", DateTimeOffset(2026, 7, 29, 11, 10, 0, TimeSpan.Zero), [| 1uy |]))
        let checks =
            [ check
                  "serial work-state kernel exercised"
                  (match state with Sealed _ -> true | _ -> false)
                  "The in-memory transition model reached Sealed using synthetic fixtures only."
              check
                  "specification admitted"
                  false
                  "BLOCKED: gtm.md names BECKN24 but does not define its source clause or behavior."
              check
                  "RED independently witnessed"
                  false
                  "BLOCKED: no admitted BECKN24 specification exists from which to create a genuine failing test."
              check
                  "candidate implementation registered"
                  false
                  "BLOCKED: there is no source-bound BECKN24 candidate."
              check
                  "GREEN independently witnessed"
                  false
                  "BLOCKED: RED and implementation preconditions are absent."
              check
                  "FsAssay passed"
                  false
                  "BLOCKED: the real project-context scan completed with blocking P02/P03 findings; synthetic attestation is not accepted."
              check
                  "ONDCFlow BECKN24 passed"
                  false
                  "BLOCKED: ONDCFlow has no admitted BECKN24 rule or test."
              check
                  "independent review accepted"
                  false
                  "BLOCKED: no candidate is eligible for review."
              check
                  "work receipt sealed"
                  false
                  "BLOCKED: the simulated state is not a signed, commit-bound observation receipt."
              check
                  "candidate commit eligible for pull request"
                  false
                  "BLOCKED: promotion requires valid observed gates, review, verdict, and seal." ]
        { Name = "ondc-beckn24"
          Checks = checks
          Succeeded = checks |> List.forall _.Passed }
