namespace CanonFlow.Assurance.Tests

open System
open Xunit
open CanonFlow.Assurance
open CanonFlow.Assurance.Signing
open CanonFlow.Assurance.Verification

module ConstructiveEvidenceTests =
    let private unwrap = function
        | Ok value -> value
        | Error error -> invalidOp error

    let private gateId value = ProofGateId.create value |> unwrap

    let private gate value =
        ProofGateReference.create
            (gateId value)
            "v1"
            (Digest.sha256Text $"implementation:{value}")
        |> unwrap

    let private requiredGates = [
        gate "cff:gate:oracle"
        gate "cff:gate:round-trip"
    ]

    let private obligation =
        Obligation.create
            (ObligationId.create "cff:test:constructive" |> unwrap)
            (Digest.sha256Text "authoritative-source")
            (Digest.sha256Text "normalized-predicate")
            requiredGates
            (ProjectionDerivation.Admitted (
                AdmissionId.create "cff:admission:test" |> unwrap
            ))
        |> unwrap

    let private evidence gateName suffix =
        ConstructiveEvidence.create
            "TestObservation"
            $"evidence/{gateName}.json"
            (Digest.sha256Text $"evidence:{gateName}:{suffix}" |> Digest.toString)
            (Some "CM4 behavioral specimen")
        |> unwrap

    let private observation
        (gateReference: ProofGateReference)
        verdict
        suffix
        : ConstructiveGateObservation
        =
        {
            GateId = ProofGateReference.gateId gateReference
            GateVersion = ProofGateReference.version gateReference
            ImplementationDigest =
                ProofGateReference.implementationDigest gateReference
            Verdict = verdict
            Evidence = [
                evidence
                    (gateReference |> ProofGateReference.gateId |> ProofGateId.value)
                    suffix
            ]
        }

    let private assess (observations: ConstructiveGateObservation list) =
        ConstructiveAssessment.create
            (Digest.sha256Text "manifest")
            obligation
            observations
        |> unwrap

    let private receipt
        verdict
        (constructive: ConstructiveAssessmentRecord list)
        : CanonFlowEvidenceReceiptV11
        =
        {
            SchemaVersion = "1.1"
            ReceiptType = "CanonFlowEvidenceReceipt"
            ReplayIdentity =
                "sha256:9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08"
            Subject = {
                Root = "."
                Schema = "constructive-test"
                SourceDirectories = []
                ManifestDigest =
                    Some "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                Artifacts = []
            }
            Evaluator = {
                EngineId = "CanonFlow.Evaluator"
                EngineVersion = "cm4-test"
            }
            Context = {
                Instant = "2026-07-28T00:00:00Z"
                TimeProvenance = "Declared"
                Locale = "invariant"
                NetworkPolicy = "Forbidden"
            }
            Assessments = []
            ConstructiveAssessments = constructive
            Verdict = verdict
            Seal = None
        }

    let private signingKeys () =
        let privateKey =
            Convert.FromHexString(
                "9d61b19deffd5a60ba844af492ec2cc44449c5697b326919703bac031cae7f60")
            |> PrivateKey.create
            |> unwrap
        let publicKey =
            Convert.FromHexString(
                "d75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a")
        privateKey, publicKey

    [<Fact>]
    let ``All required constructive gates produce promotable Pass`` () =
        let assessment =
            requiredGates
            |> List.map (fun required ->
                observation required Verdict.Pass "pass")
            |> assess
        Assert.Equal(Verdict.Pass, assessment.Verdict)
        Assert.Equal(2, assessment.RequiredGates)
        Assert.Equal(2, assessment.EvaluatedGates)
        Assert.Empty(assessment.MissingGateIds)
        Assert.True(ConstructiveAssessment.isPromotable assessment)
        Assert.Equal(0, ExitCode.ofVerdict assessment.Verdict)

    [<Fact>]
    let ``Missing constructive evidence is explicit and never exit zero`` () =
        let assessment =
            [observation requiredGates.Head Verdict.Pass "partial"]
            |> assess
        Assert.Equal(Verdict.Inconclusive, assessment.Verdict)
        Assert.Equal<string list>(
            ["cff:gate:round-trip"],
            assessment.MissingGateIds)
        Assert.False(ConstructiveAssessment.isPromotable assessment)
        Assert.Equal(2, ExitCode.ofVerdict assessment.Verdict)

    [<Fact>]
    let ``Removing a failed gate cannot improve promotability`` () =
        let failed =
            [
                observation requiredGates.Head Verdict.Pass "pass"
                observation requiredGates.Tail.Head Verdict.Fail "fail"
            ]
            |> assess
        let removed =
            [observation requiredGates.Head Verdict.Pass "pass"]
            |> assess
        Assert.Equal(Verdict.Fail, failed.Verdict)
        Assert.Equal(Verdict.Inconclusive, removed.Verdict)
        Assert.False(ConstructiveAssessment.isPromotable failed)
        Assert.False(ConstructiveAssessment.isPromotable removed)

    [<Fact>]
    let ``Cumulative duplicate gate observations retain the worst verdict`` () =
        let assessment =
            [
                observation requiredGates.Head Verdict.Pass "earlier"
                observation requiredGates.Head Verdict.Fail "later"
                observation requiredGates.Tail.Head Verdict.Pass "pass"
            ]
            |> assess
        Assert.Equal(Verdict.Fail, assessment.Verdict)
        let oracle =
            assessment.Gates
            |> List.find (fun gate -> gate.GateId = "cff:gate:oracle")
        Assert.Equal(Verdict.Fail, oracle.Verdict)
        Assert.Equal(2, oracle.Evidence.Length)

    [<Fact>]
    let ``Sealing Fail and Inconclusive never changes their verdict`` () =
        let privateKey, publicKey = signingKeys ()
        let failedAssessment =
            [
                observation requiredGates.Head Verdict.Fail "fail"
                observation requiredGates.Tail.Head Verdict.Pass "pass"
            ]
            |> assess
        let incompleteAssessment =
            [observation requiredGates.Head Verdict.Pass "partial"]
            |> assess
        for verdict, assessment in [
            Verdict.Fail, failedAssessment
            Verdict.Inconclusive, incompleteAssessment
        ] do
            let signed =
                receipt verdict [assessment]
                |> ReceiptVerifier.signReceipt "rfc8032:test-1" privateKey
            Assert.Equal(verdict, signed.Verdict)
            Assert.True(ReceiptVerifier.verifyReceipt publicKey signed |> Result.isOk)
            Assert.True(
                ReceiptVerifier.verifyEnvelopeJson
                    (CanonicalReceiptJson.serializeEnvelope signed)
                    (Some publicKey)
                    false
                |> Result.isOk)

    [<Fact>]
    let ``Signed receipt cannot promote a failed constructive gate`` () =
        let privateKey, publicKey = signingKeys ()
        let failed =
            [
                observation requiredGates.Head Verdict.Fail "fail"
                observation requiredGates.Tail.Head Verdict.Pass "pass"
            ]
            |> assess
        let dishonest =
            receipt Verdict.Pass [failed]
            |> ReceiptVerifier.signReceipt "rfc8032:test-1" privateKey
            |> CanonicalReceiptJson.serializeEnvelope
        Assert.True(
            ReceiptVerifier.verifyEnvelopeJson dishonest (Some publicKey) false
            |> Result.isError)
