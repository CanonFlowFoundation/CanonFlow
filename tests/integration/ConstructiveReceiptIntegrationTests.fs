namespace Canon.IntegrationTests

open System
open System.IO
open Xunit
open CanonFlow.Assurance
open CanonFlow.Assurance.Verification
open CanonFlow.Evaluator

module ConstructiveReceiptIntegrationTests =
    let private repositoryRoot =
        Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "..", ".."))

    let private manifest name =
        Path.Combine(
            repositoryRoot,
            "examples",
            "constructive-cm4",
            name)

    let private evaluate name =
        match Pipeline.evaluate (manifest name) with
        | Ok run -> run
        | Error error -> invalidOp error

    let private publicKey =
        Convert.FromHexString(
            "d75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a")

    [<Fact>]
    let ``Constructive Pass is separate complete and offline verifiable`` () =
        let run = evaluate "canonflow-evaluation.json"
        Assert.Equal(Verdict.Pass, run.Receipt.Verdict)
        Assert.Equal(0, run.ExitCode)
        Assert.Empty(run.Receipt.Assessments)
        let constructive = Assert.Single(run.Receipt.ConstructiveAssessments)
        Assert.Equal("cff:lab:required-contact", constructive.ObligationId)
        Assert.Equal("Admitted", constructive.ProjectionState)
        Assert.Equal("Admitted", constructive.DerivationKind)
        Assert.Equal(
            Some "cff:admission:cm2-required-contact-lab",
            constructive.DerivationReference)
        Assert.Equal(
            "sha256:8a71fd4510146dbd2bf2822eef5b7934bfef70612b3fa1ad97d69d5938c2bded",
            constructive.SourceDigest)
        Assert.Equal(4, constructive.RequiredGates)
        Assert.Equal(4, constructive.EvaluatedGates)
        Assert.Empty(constructive.MissingGateIds)
        Assert.True(ConstructiveAssessment.isPromotable constructive)
        Assert.True(
            ReceiptVerifier.verifyEnvelopeJson
                run.CanonicalReceipt
                (Some publicKey)
                false
            |> Result.isOk)

    [<Fact>]
    let ``Signed failed constructive gate remains Fail`` () =
        let run = evaluate "canonflow-evaluation.fail.json"
        Assert.Equal(Verdict.Fail, run.Receipt.Verdict)
        Assert.Equal(1, run.ExitCode)
        let constructive = Assert.Single(run.Receipt.ConstructiveAssessments)
        Assert.Equal(Verdict.Fail, constructive.Verdict)
        Assert.False(ConstructiveAssessment.isPromotable constructive)
        Assert.True(
            ReceiptVerifier.verifyEnvelopeJson
                run.CanonicalReceipt
                (Some publicKey)
                false
            |> Result.isOk)

    [<Fact>]
    let ``Missing constructive bundle is explicit Inconclusive`` () =
        let run = evaluate "canonflow-evaluation.missing.json"
        Assert.Equal(Verdict.Inconclusive, run.Receipt.Verdict)
        Assert.Equal(2, run.ExitCode)
        let constructive = Assert.Single(run.Receipt.ConstructiveAssessments)
        Assert.Equal(0, constructive.EvaluatedGates)
        Assert.Equal(4, constructive.MissingGateIds.Length)
        Assert.False(ConstructiveAssessment.isPromotable constructive)
        Assert.True(
            ReceiptVerifier.verifyEnvelopeJson
                run.CanonicalReceipt
                (Some publicKey)
                false
            |> Result.isOk)
