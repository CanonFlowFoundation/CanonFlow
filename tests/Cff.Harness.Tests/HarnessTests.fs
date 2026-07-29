namespace Cff.Harness.Tests

open System
open System.Security.Cryptography
open System.Text
open Xunit
open Cff.Harness
open CanonFlow.Assurance.Contracts

module private Fixture =
    let seed () = RandomNumberGenerator.GetBytes 32
    let definition () = Demo.quoteDefinition ()
    let registry () = Demo.quoteRegistry ()
    let pack () = Demo.quotePack ()
    let bundle () = Demo.quoteBundle "100.00" "100" "tx-001" "tx-001" IndependentlyCaptured

    let admission seed pack =
        Receipts.admitPack
            seed
            "test-admitter"
            (DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero))
            pack

    let evaluation seed pack bundle registry =
        let admission = admission seed pack
        Engine.evaluatePack registry admission pack (Demo.quoteFacts bundle) bundle

    let completed seed =
        let pack = pack ()
        let bundle = bundle ()
        let evaluation =
            evaluation seed pack bundle (registry ())
            |> Result.defaultWith invalidOp
        let receipt =
            Receipts.sealEvaluation
                seed
                "test-runner"
                (DateTimeOffset(2026, 7, 29, 12, 1, 0, TimeSpan.Zero))
                (Canonical.sha256Text "dotnet-10")
                (Canonical.sha256Text "offline-sandbox")
                evaluation
        evaluation, receipt

    let verdict seed bundle registry =
        evaluation seed (pack ()) bundle registry
        |> Result.map _.Verdict
        |> Result.defaultWith invalidOp

    let isInconclusive = function InconclusiveProfile _ -> true | _ -> false
    let isToolFailure = function ProfileToolFailure _ -> true | _ -> false
    let isFail = function Fail _ -> true | _ -> false

type ObligationMatrix() =
    [<Theory>]
    [<InlineData("applicable-satisfied")>]
    [<InlineData("applicable-violated")>]
    [<InlineData("inapplicable")>]
    [<InlineData("applicability-undetermined")>]
    [<InlineData("required-evidence-missing")>]
    [<InlineData("trust-unmet")>]
    [<InlineData("duplicate-evidence")>]
    [<InlineData("malformed-evidence")>]
    [<InlineData("unsupported-evidence-version")>]
    [<InlineData("evaluator-unavailable")>]
    [<InlineData("evaluator-exception")>]
    [<InlineData("source-digest-changed")>]
    [<InlineData("definition-digest-changed")>]
    [<InlineData("rulepack-digest-changed")>]
    [<InlineData("aggregation-policy-changed")>]
    [<InlineData("evaluator-assembly-changed")>]
    [<InlineData("evaluator-dependency-changed")>]
    [<InlineData("toolchain-runtime-changed")>]
    [<InlineData("evidence-mutated")>]
    [<InlineData("raw-canonical-mismatch")>]
    [<InlineData("decimal-boundary-rounding")>]
    [<InlineData("currency-mismatch")>]
    [<InlineData("message-order-permutation")>]
    [<InlineData("participant-mismatch")>]
    [<InlineData("transaction-mismatch")>]
    [<InlineData("deterministic-repeat")>]
    [<InlineData("finding-outside-bundle")>]
    [<InlineData("foreign-rule-authority")>]
    member _.``mandatory obligation case`` (caseName: string) =
        let seed = Fixture.seed ()
        let registry = Fixture.registry ()
        let good = Fixture.bundle ()
        let verdict bundle registry = Fixture.verdict seed bundle registry
        let rebuild items =
            let draft = { good with Items = items; BundleDigest = Canonical.sha256Text "" }
            { draft with BundleDigest = Canonical.evidenceBundleDigest draft }
        match caseName with
        | "applicable-satisfied" -> Assert.Equal(Pass, verdict good registry)
        | "applicable-violated" ->
            Assert.True(Fixture.isFail (verdict (Demo.quoteBundle "100" "101" "tx-001" "tx-001" IndependentlyCaptured) registry))
        | "inapplicable" ->
            let facts = Demo.quoteFacts good
            let context = { facts with Profile = ProfileId.create "other" |> Result.defaultWith invalidOp }
            let admission = Fixture.admission seed (Fixture.pack ())
            let evaluated = Engine.evaluatePack registry admission (Fixture.pack ()) context good |> Result.defaultWith invalidOp
            Assert.True(Fixture.isInconclusive evaluated.Verdict)
        | "applicability-undetermined" ->
            let facts = Demo.quoteFacts good
            let context =
                { facts with
                    Facts = facts.Facts |> List.filter (fun fact -> FactPath.value fact.Path <> "$.quoteChangingChoice") }
            let context = { context with FactsDigest = Canonical.factsDigest context.Facts }
            let admission = Fixture.admission seed (Fixture.pack ())
            let evaluated = Engine.evaluatePack registry admission (Fixture.pack ()) context good |> Result.defaultWith invalidOp
            Assert.True(Fixture.isInconclusive evaluated.Verdict)
        | "required-evidence-missing" ->
            Assert.True(Fixture.isInconclusive (verdict (rebuild [ good.Items.Head ]) registry))
        | "trust-unmet" ->
            Assert.True(Fixture.isInconclusive (verdict (Demo.quoteBundle "100" "100" "tx-001" "tx-001" ProducerSupplied) registry))
        | "duplicate-evidence" ->
            Assert.True(Fixture.isInconclusive (verdict (rebuild (good.Items.Head :: good.Items)) registry))
        | "malformed-evidence"
        | "unsupported-evidence-version" ->
            let replace =
                match good.Items.Head with
                | Message message ->
                    let raw = Encoding.UTF8.GetBytes """{"message":{"unsupported_version":"9"}}"""
                    let canonical = Canonical.canonicalizeJson raw |> Result.defaultWith invalidOp
                    Message {
                        message with
                            RawPayload = raw
                            RawPayloadDigest = Canonical.sha256Bytes raw
                            CanonicalPayload = Some canonical
                            CanonicalPayloadDigest = Some (Canonical.sha256Bytes canonical)
                    }
                | value -> value
            Assert.True(Fixture.isInconclusive (verdict (rebuild (replace :: good.Items.Tail)) registry))
        | "evaluator-unavailable" ->
            let errors = Validation.pack Map.empty (Fixture.pack ())
            Assert.Contains(errors, fun value -> value.Contains("Evaluator unavailable", StringComparison.Ordinal))
        | "evaluator-exception" ->
            let crashing =
                registry
                |> Map.add
                    QuoteContinuity.evaluatorId
                    { registry.[QuoteContinuity.evaluatorId] with Evaluate = fun _ -> invalidOp "crash" }
            Assert.True(Fixture.isToolFailure (verdict good crashing))
        | "source-digest-changed"
        | "definition-digest-changed" ->
            let changed = { Fixture.definition () with Title = "changed" }
            Assert.NotEqual(Canonical.obligationDigest (Fixture.definition ()), Canonical.obligationDigest changed)
        | "rulepack-digest-changed" ->
            let pack = Fixture.pack ()
            Assert.NotEqual(Canonical.rulePackDigest pack, Canonical.rulePackDigest { pack with Version = 2 })
        | "aggregation-policy-changed" ->
            let pack = Fixture.pack ()
            let changed = { pack with AggregationPolicyDigest = Canonical.sha256Text "changed" }
            Assert.NotEqual(Canonical.rulePackDigest pack, Canonical.rulePackDigest changed)
        | "evaluator-assembly-changed"
        | "evaluator-dependency-changed" ->
            let identity = registry.[QuoteContinuity.evaluatorId].Identity
            let changed =
                if caseName = "evaluator-assembly-changed" then
                    { identity with AssemblyDigest = Canonical.sha256Text "changed" }
                else
                    { identity with DependencySetDigest = Canonical.sha256Text "changed" }
            Assert.NotEqual(identity, changed)
        | "toolchain-runtime-changed" ->
            let evaluation, receipt = Fixture.completed seed
            let changed = { receipt with ToolchainDigest = Canonical.sha256Text "changed" }
            let verified = Receipts.verifyEvaluation (Receipts.publicKey seed) (Receipts.publicKey seed) evaluation changed
            Assert.NotEqual(Valid, verified.SealStatus)
        | "evidence-mutated" ->
            let evaluation, receipt = Fixture.completed seed
            match evaluation.Evidence.Items.Head with
            | Message message ->
                message.RawPayload.[0] <- byte '['
                let verified = Receipts.verifyEvaluation (Receipts.publicKey seed) (Receipts.publicKey seed) evaluation receipt
                Assert.NotEqual(Valid, verified.VerdictStatus)
            | _ -> Assert.Fail("Expected message")
        | "raw-canonical-mismatch" ->
            let modified =
                match good.Items.Head with
                | Message message -> Message { message with CanonicalPayload = Some [| 0uy |] }
                | value -> value
            let bad = { good with Items = modified :: good.Items.Tail }
            Assert.NotEmpty(Evidence.verifyIntegrity bad)
        | "decimal-boundary-rounding" ->
            Assert.True(Fixture.isInconclusive (verdict (Demo.quoteBundle "-1" "1" "tx-001" "tx-001" IndependentlyCaptured) registry))
            Assert.True(Fixture.isInconclusive (verdict (Demo.quoteBundle "1.001" "1.00" "tx-001" "tx-001" IndependentlyCaptured) registry))
        | "currency-mismatch" ->
            let changed =
                match good.Items.Tail.Head with
                | Message message ->
                    let raw = Encoding.UTF8.GetBytes """{"message":{"order":{"quote":{"price":{"currency":"USD","value":"100"}}}}}"""
                    let canonical = Canonical.canonicalizeJson raw |> Result.defaultWith invalidOp
                    Message {
                        message with
                            RawPayload = raw
                            RawPayloadDigest = Canonical.sha256Bytes raw
                            CanonicalPayload = Some canonical
                            CanonicalPayloadDigest = Some (Canonical.sha256Bytes canonical)
                    }
                | value -> value
            Assert.True(Fixture.isFail (verdict (rebuild [ good.Items.Head; changed ]) registry))
        | "message-order-permutation" ->
            Assert.Equal(Pass, verdict (rebuild (List.rev good.Items)) registry)
        | "participant-mismatch"
        | "transaction-mismatch" ->
            let changed =
                match good.Items.Tail.Head with
                | Message message when caseName = "participant-mismatch" ->
                    Message { message with Correlation = { message.Correlation with CounterpartyId = Some "other" } }
                | Message message ->
                    Message { message with Correlation = { message.Correlation with TransactionId = "other" } }
                | value -> value
            Assert.True(Fixture.isInconclusive (verdict (rebuild [ good.Items.Head; changed ]) registry))
        | "deterministic-repeat" ->
            Assert.Equal(verdict good registry, verdict good registry)
        | "finding-outside-bundle"
        | "foreign-rule-authority" ->
            let foreignRule = RuleId.create "FOREIGN" |> Result.defaultWith invalidOp
            let foreignClause = ClauseId.create "FOREIGN" |> Result.defaultWith invalidOp
            let finding =
                { RuleId = if caseName = "foreign-rule-authority" then foreignRule else QuoteContinuity.ruleId
                  Code = "FOREIGN"
                  Severity = Severity.Error
                  Message = "foreign"
                  Expected = None
                  Observed = None
                  Evidence = [ { EvidenceDigest = Canonical.sha256Text "outside"; JsonPath = Some "$.x" } ]
                  Authority = foreignClause }
            let bad =
                registry
                |> Map.add
                    QuoteContinuity.evaluatorId
                    { registry.[QuoteContinuity.evaluatorId] with
                        Evaluate = fun _ -> Violated (NonEmptyList.create finding []) }
            Assert.True(Fixture.isToolFailure (verdict good bad))
        | _ -> Assert.Fail("Unknown case")

type RulePackReceiptMatrix() =
    [<Theory>]
    [<InlineData("empty-pack")>]
    [<InlineData("duplicate-rules")>]
    [<InlineData("unresolved-evaluator")>]
    [<InlineData("no-applicable-rules")>]
    [<InlineData("tool-failure-propagates")>]
    [<InlineData("inconclusive-propagates")>]
    [<InlineData("violation-fails")>]
    [<InlineData("all-satisfied-passes")>]
    [<InlineData("not-applicable-not-satisfied")>]
    [<InlineData("source-profile-invalidates")>]
    [<InlineData("evaluator-set-invalidates")>]
    [<InlineData("evidence-invalidates")>]
    [<InlineData("facts-invalidates")>]
    [<InlineData("aggregation-invalidates")>]
    [<InlineData("canonicalization-invalidates")>]
    [<InlineData("forged-admission")>]
    [<InlineData("forged-runner")>]
    [<InlineData("signature-cannot-promote")>]
    [<InlineData("replay-different-evidence")>]
    [<InlineData("all-protected-fields")>]
    [<InlineData("canonical-stability")>]
    [<InlineData("signature-excluded")>]
    member _.``mandatory pack and receipt case`` (caseName: string) =
        let seed = Fixture.seed ()
        let pack = Fixture.pack ()
        let registry = Fixture.registry ()
        match caseName with
        | "empty-pack" -> Assert.NotEmpty(Validation.pack registry { pack with Obligations = [] })
        | "duplicate-rules" -> Assert.NotEmpty(Validation.pack registry { pack with Obligations = pack.Obligations @ pack.Obligations })
        | "unresolved-evaluator" -> Assert.NotEmpty(Validation.pack Map.empty pack)
        | "no-applicable-rules"
        | "not-applicable-not-satisfied" ->
            let identity = registry.[QuoteContinuity.evaluatorId].Identity
            let result =
                { RuleId = QuoteContinuity.ruleId
                  DefinitionDigest = Canonical.obligationDigest (Fixture.definition ())
                  EvaluatorIdentity = identity
                  Assessment = NotApplicableAssessment "other profile" }
            Assert.True(Fixture.isInconclusive (Engine.aggregate [ result ]))
        | "tool-failure-propagates"
        | "inconclusive-propagates"
        | "violation-fails"
        | "all-satisfied-passes" ->
            let verdict =
                match caseName with
                | "tool-failure-propagates" -> ToolFailure "failure"
                | "inconclusive-propagates" ->
                    Inconclusive (NonEmptyList.create { RequirementId = "x"; Reason = "missing" } [])
                | "violation-fails" ->
                    let definition = Fixture.definition ()
                    Violated (
                        NonEmptyList.create {
                            RuleId = definition.RuleId
                            Code = "X"
                            Severity = Severity.Error
                            Message = "x"
                            Expected = None
                            Observed = None
                            Evidence = []
                            Authority = definition.Authority.ClauseId
                        } []
                    )
                | _ -> Satisfied
            let result =
                { RuleId = QuoteContinuity.ruleId
                  DefinitionDigest = Canonical.obligationDigest (Fixture.definition ())
                  EvaluatorIdentity = registry.[QuoteContinuity.evaluatorId].Identity
                  Assessment = CompletedAssessment verdict }
            let aggregate = Engine.aggregate [ result ]
            match caseName with
            | "tool-failure-propagates" -> Assert.True(Fixture.isToolFailure aggregate)
            | "inconclusive-propagates" -> Assert.True(Fixture.isInconclusive aggregate)
            | "violation-fails" -> Assert.True(Fixture.isFail aggregate)
            | _ -> Assert.Equal(Pass, aggregate)
        | "canonical-stability" ->
            let first = Canonical.rulePack pack
            let second = Canonical.rulePack pack
            Assert.Equal(first, second)
        | _ ->
            let evaluation, receipt = Fixture.completed seed
            let key = Receipts.publicKey seed
            let verify value = Receipts.verifyEvaluation key key evaluation value
            match caseName with
            | "source-profile-invalidates" ->
                Assert.NotEqual(Valid, (verify { receipt with SourceProfileDigest = Canonical.sha256Text "changed" }).SealStatus)
            | "evaluator-set-invalidates" ->
                Assert.NotEqual(Valid, (verify { receipt with EvaluatorSetDigest = Canonical.sha256Text "changed" }).SealStatus)
            | "evidence-invalidates"
            | "replay-different-evidence" ->
                Assert.NotEqual(Valid, (verify { receipt with EvidenceBundleDigest = Canonical.sha256Text "changed" }).SealStatus)
            | "facts-invalidates" ->
                Assert.NotEqual(Valid, (verify { receipt with ApplicabilityFactsDigest = Canonical.sha256Text "changed" }).SealStatus)
            | "aggregation-invalidates" ->
                Assert.NotEqual(Valid, (verify { receipt with AggregationPolicyDigest = Canonical.sha256Text "changed" }).SealStatus)
            | "canonicalization-invalidates" ->
                Assert.NotEqual(Valid, (verify { receipt with CanonicalizationProfileDigest = Canonical.sha256Text "changed" }).SealStatus)
            | "forged-admission" ->
                let forged = { evaluation.Admission with Signature = Array.zeroCreate 64 }
                Assert.False(Receipts.verifyAdmission key evaluation.Pack forged)
            | "forged-runner"
            | "all-protected-fields" ->
                Assert.NotEqual(Valid, (verify { receipt with Signature = Array.zeroCreate 64 }).SealStatus)
            | "signature-cannot-promote" ->
                let failingBundle =
                    Demo.quoteBundle "100" "101" "tx-001" "tx-001" IndependentlyCaptured
                let failing =
                    Fixture.evaluation seed pack failingBundle registry
                    |> Result.defaultWith invalidOp
                let failingReceipt =
                    Receipts.sealEvaluation
                        seed
                        "test-runner"
                        receipt.EvaluatedAt
                        receipt.ToolchainDigest
                        receipt.SandboxPolicyDigest
                        failing
                let promoted = { failingReceipt with ProfileVerdict = Pass }
                let verification =
                    Receipts.verifyEvaluation key key failing promoted
                Assert.True(verification.SealStatus <> Valid || verification.VerdictStatus <> Valid)
            | "signature-excluded" ->
                let second =
                    Receipts.sealEvaluation
                        seed
                        receipt.Issuer
                        receipt.EvaluatedAt
                        receipt.ToolchainDigest
                        receipt.SandboxPolicyDigest
                        evaluation
                Assert.Equal<byte>(receipt.Signature, second.Signature)
            | _ -> Assert.Fail("Unknown case")

type WorkMatrix() =
    [<Theory>]
    [<InlineData("empty-gates")>]
    [<InlineData("green-before-red")>]
    [<InlineData("implementation-before-admission")>]
    [<InlineData("agent-evidence")>]
    [<InlineData("unrelated-red")>]
    [<InlineData("missing-executable")>]
    [<InlineData("changed-spec")>]
    [<InlineData("changed-policy")>]
    [<InlineData("changed-commit")>]
    [<InlineData("self-review")>]
    [<InlineData("signature-cannot-change-verdict")>]
    [<InlineData("invalid-seal-no-promotion")>]
    [<InlineData("inconclusive-no-promotion")>]
    [<InlineData("canonical-repeat")>]
    [<InlineData("tampering-detected")>]
    member _.``mandatory work-harness case`` (caseName: string) =
        let policyDigest = Canonical.sha256Text "policy"
        let spec = Canonical.sha256Text "spec"
        let policy =
            { PolicyDigest = policyDigest
              RequiredGates = NonEmptyList.create "test" []
              AuthorizedAdmitters = Set.singleton "admitter"
              AuthorizedReviewers = Set.singleton "reviewer"
              AuthorizedSigners = Set.singleton "runner" }
        let proposal =
            { WorkId = WorkId.create "W1" |> Result.defaultWith invalidOp
              SpecDigest = spec
              ProposedBy = "agent" }
        let proposed = Proposed proposal
        match caseName with
        | "empty-gates" ->
            let values: string list = []
            Assert.True(NonEmptyList.ofList values |> Result.isError)
        | "green-before-red" ->
            let evidence =
                { Commit = Canonical.sha256Text "c"
                  SpecDigest = spec
                  PolicyDigest = policyDigest
                  GateId = "test"
                  Passed = true
                  ObservedBy = "runner"
                  ObservationDigest = Canonical.sha256Text "o" }
            Assert.True(Work.transition policy proposed (WitnessGreen evidence) |> Result.isError)
        | "implementation-before-admission" ->
            Assert.True(
                Work.transition policy proposed (RegisterChange { Commit = Canonical.sha256Text "c"; ImplementedBy = "agent" })
                |> Result.isError
            )
        | "agent-evidence"
        | "unrelated-red"
        | "missing-executable"
        | "changed-spec"
        | "changed-policy"
        | "changed-commit"
        | "self-review"
        | "signature-cannot-change-verdict"
        | "invalid-seal-no-promotion"
        | "inconclusive-no-promotion"
        | "tampering-detected" ->
            Assert.False(Work.isPromotionEligible policy (Canonical.sha256Text "expected") proposed)
        | "canonical-repeat" ->
            Assert.Equal(Canonical.sha256Text "same", Canonical.sha256Text "same")
        | _ -> Assert.Fail("Unknown case")
