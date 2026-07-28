namespace CanonFlow.Assurance.Tests

open System
open System.Text
open Xunit
open FsCheck.Xunit
open CanonFlow.Assurance

module ObligationManifestTests =
    let private unwrap = function
        | Ok value -> value
        | Error error -> invalidOp error

    let private obligationId value = ObligationId.create value |> unwrap
    let private gateId value = ProofGateId.create value |> unwrap
    let private assumptionId value = AssumptionId.create value |> unwrap
    let private admissionId value = AdmissionId.create value |> unwrap

    let private gate value =
        ProofGateReference.create
            (gateId value)
            "v1"
            (Digest.sha256Text $"implementation:{value}")
        |> unwrap

    let private obligation source derivation =
        Obligation.create
            (obligationId "cff:test:required-contact")
            (Digest.sha256Text source)
            (Digest.sha256Text "normalized:email-or-phone")
            [gate "cff:gate:oracle"; gate "cff:gate:mutation"]
            derivation
        |> unwrap

    let private manifest policy source derivation =
        ObligationManifest.create
            (Digest.sha256Text policy)
            [obligation source derivation]
        |> unwrap

    [<Fact>]
    let ``Empty obligation and gate lists are rejected`` () =
        Assert.True(
            ObligationManifest.create (Digest.sha256Text "policy") []
            |> Result.isError)
        Assert.True(
            Obligation.create
                (obligationId "cff:test:empty")
                (Digest.sha256Text "source")
                (Digest.sha256Text "predicate")
                []
                ProjectionDerivation.None
            |> Result.isError)

    [<Fact>]
    let ``Empty and incomplete gate results are Inconclusive`` () =
        let admitted =
            obligation
                "source"
                (ProjectionDerivation.Admitted (admissionId "cff:admission:test"))
        Assert.Equal(Verdict.Inconclusive, Projection.evaluate [] admitted)
        Assert.Equal(
            Verdict.Inconclusive,
            Projection.evaluate [gateId "cff:gate:oracle", Verdict.Pass] admitted)
        Assert.Equal(
            Verdict.Pass,
            Projection.evaluate [
                gateId "cff:gate:oracle", Verdict.Pass
                gateId "cff:gate:mutation", Verdict.Pass
            ] admitted)

    [<Fact>]
    let ``Candidate derivation cannot produce Pass`` () =
        let candidate =
            obligation
                "source"
                (ProjectionDerivation.Candidate (
                    NonEmpty.create
                        (assumptionId "cff:assumption:policy-approval")
                        []
                ))
        let verdict =
            Projection.evaluate [
                gateId "cff:gate:oracle", Verdict.Pass
                gateId "cff:gate:mutation", Verdict.Pass
            ] candidate
        Assert.Equal(Verdict.Inconclusive, verdict)
        let json =
            manifest
                "policy"
                "source"
                (Obligation.derivation candidate)
            |> ObligationManifest.serialize
        Assert.Contains("\"state\":\"CandidateRequiringApproval\"", json)
        Assert.DoesNotContain("\"state\":\"Exact\"", json)

    [<Fact>]
    let ``Canonical manifest is deterministic and round trips`` () =
        let first =
            manifest
                "policy"
                "source"
                (ProjectionDerivation.Admitted (admissionId "cff:admission:test"))
        let firstJson = ObligationManifest.serialize first
        let secondJson = ObligationManifest.serialize first
        Assert.Equal(firstJson, secondJson)
        Assert.Equal(
            """{"manifestType":"CanonFlowObligationManifest","obligations":[{"id":"cff:test:required-contact","normalizedPredicateDigest":"sha256:96bac9c0349a6b6b71720c29a785034770bd0dab381ac83fa041fca1eb6145f7","projection":{"derivation":{"admissionId":"cff:admission:test","kind":"Admitted"},"state":"Admitted"},"requiredGates":[{"id":"cff:gate:mutation","implementationDigest":"sha256:18902edacd5af0f16268f8a2d8095c76d286147920eaa188b4246d31908af56d","version":"v1"},{"id":"cff:gate:oracle","implementationDigest":"sha256:4e0e3f26aed0b56dae07bb46fadfda29f01a3132762084ab604c1a134db7b443","version":"v1"}],"sourceDigest":"sha256:41cf6794ba4200b839c53531555f0f3998df4cbb01a4d5cb0b94e3ca5e23947d"}],"policyDigest":"sha256:823412d1eacb67956220e532959f0104603057c88704863ca38e7cd188fda812","protectedDigest":"sha256:acfa5d5a88d5c73c966d3007f7ecf579675577f0bd5cd1a293aa6dc92b10aaaa","schemaVersion":"1.0"}""",
            firstJson)
        let parsed =
            firstJson
            |> Encoding.UTF8.GetBytes
            |> ObligationManifest.parseBytes
            |> unwrap
        Assert.Equal(firstJson, ObligationManifest.serialize parsed)

    [<Fact>]
    let ``Policy and source changes alter protected digest`` () =
        let derivation =
            ProjectionDerivation.Admitted (admissionId "cff:admission:test")
        let baseline = manifest "policy-a" "source-a" derivation
        let changedPolicy = manifest "policy-b" "source-a" derivation
        let changedSource = manifest "policy-a" "source-b" derivation
        let digest value =
            value
            |> ObligationManifest.protectedDigest
            |> Digest.toString
        Assert.False(digest baseline = digest changedPolicy)
        Assert.False(digest baseline = digest changedSource)

    [<Fact>]
    let ``Protected digest mutation is rejected`` () =
        let json =
            manifest "policy" "source" ProjectionDerivation.None
            |> ObligationManifest.serialize
        let protectedDigest =
            json.Substring(
                json.IndexOf("\"protectedDigest\":\"", StringComparison.Ordinal) + 20,
                71
            )
        let replacement =
            if protectedDigest.EndsWith("0", StringComparison.Ordinal) then
                protectedDigest.Substring(0, 70) + "1"
            else
                protectedDigest.Substring(0, 70) + "0"
        let tampered = json.Replace(protectedDigest, replacement, StringComparison.Ordinal)
        Assert.True(
            tampered
            |> Encoding.UTF8.GetBytes
            |> ObligationManifest.parseBytes
            |> Result.isError)

    [<Property>]
    let ``Arbitrary byte parsing is total`` (bytes: byte[]) =
        try
            ObligationManifest.parseBytes bytes |> ignore
            true
        with _ ->
            false

    [<Fact>]
    let ``Four verdict exit mapping is explicit and invertible`` () =
        let mappings = [
            Verdict.Pass, 0
            Verdict.Fail, 1
            Verdict.Inconclusive, 2
            Verdict.ToolFailure, 3
        ]
        for verdict, exitCode in mappings do
            Assert.Equal(exitCode, ExitCode.ofVerdict verdict)
            Assert.Equal(Some verdict, ExitCode.tryVerdict exitCode)
        Assert.Equal(None, ExitCode.tryVerdict ExitCode.InvalidInvocation)
