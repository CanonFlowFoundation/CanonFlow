module Tests

open Xunit
open CanonFlow.Assurance
open CanonFlow.Evaluator

[<Fact>]
let ``Evaluator exit mapping preserves all four public states`` () =
    Assert.Equal(0, CanonFlow.Evaluator.ComponentRunner.mapExitCodeToVerdict 0 |> Verdict.rank)
    Assert.Equal(Verdict.Fail, CanonFlow.Evaluator.ComponentRunner.mapExitCodeToVerdict 1)
    Assert.Equal(Verdict.Inconclusive, CanonFlow.Evaluator.ComponentRunner.mapExitCodeToVerdict 2)
    Assert.Equal(Verdict.ToolFailure, CanonFlow.Evaluator.ComponentRunner.mapExitCodeToVerdict 3)

[<Fact>]
let ``Pipeline derives all four verdicts without collapsing assessment axes`` () =
    let assessment health compliance applicable evaluated = {
        ComponentId = "fixture"
        ComponentVersion = "1"
        Health = health
        Compliance = compliance
        ApplicableRules = applicable
        EvaluatedRules = evaluated
        Evidence = []
    }
    let finding = NonEmpty.create { Description = "violation" } []
    let gap = NonEmpty.create { Description = "missing evidence" } []
    Assert.Equal(Verdict.Pass, CanonFlow.Evaluator.Pipeline.verdictOf (assessment EvidenceHealth.Complete Compliance.Conformant 1 1))
    Assert.Equal(Verdict.Fail, CanonFlow.Evaluator.Pipeline.verdictOf (assessment EvidenceHealth.Complete (Compliance.NonConformant finding) 1 1))
    Assert.Equal(Verdict.Inconclusive, CanonFlow.Evaluator.Pipeline.verdictOf (assessment (EvidenceHealth.Partial gap) Compliance.NotEstablished 1 0))
    Assert.Equal(Verdict.ToolFailure, CanonFlow.Evaluator.Pipeline.verdictOf (assessment (EvidenceHealth.Broken { Description = "crash" }) Compliance.NotEstablished 1 0))

[<Fact>]
let ``Every EvidenceHealth and Compliance product has explicit verdict semantics`` () =
    let gap = NonEmpty.create { Description = "missing" } []
    let finding = NonEmpty.create { Description = "violation" } []
    let healthCases = [
        EvidenceHealth.Complete
        EvidenceHealth.Partial gap
        EvidenceHealth.Broken { Description = "broken" }
    ]
    let complianceCases = [
        Compliance.Conformant
        Compliance.NonConformant finding
        Compliance.NotEstablished
    ]
    let assessment health compliance = {
        ComponentId = "fixture"
        ComponentVersion = "1"
        Health = health
        Compliance = compliance
        ApplicableRules = 1
        EvaluatedRules = 1
        Evidence = []
    }
    let combinations = [
        for health in healthCases do
            for compliance in complianceCases do
                yield health, compliance, Pipeline.verdictOf (assessment health compliance)
    ]
    Assert.Equal(9, combinations.Length)
    for health, compliance, verdict in combinations do
        match health, compliance with
        | EvidenceHealth.Broken _, _ -> Assert.Equal(Verdict.ToolFailure, verdict)
        | _, Compliance.NonConformant _ -> Assert.Equal(Verdict.Fail, verdict)
        | EvidenceHealth.Complete, Compliance.Conformant -> Assert.Equal(Verdict.Pass, verdict)
        | _ -> Assert.Equal(Verdict.Inconclusive, verdict)

[<Fact>]
let ``Manifest parser rejects unknown semantic fields`` () =
    let manifest = """
{
  "subject": { "root": ".", "artifacts": [] },
  "profiles": ["fsassay-production-v1"],
  "evaluationContext": {
    "instant": "2026-07-27T00:00:00Z",
    "timeProvenance": "Declared",
    "network": "Forbidden",
    "locale": "invariant"
  },
  "mutableAlias": "production"
}
"""
    Assert.True(ManifestParser.parse manifest |> Result.isError)

[<Fact>]
let ``Manifest parser rejects incoherent budgets`` () =
    let manifest = """
{
  "subject": { "root": ".", "artifacts": [] },
  "profiles": ["fsassay-production-v1"],
  "evaluationContext": {
    "instant": "2026-07-27T00:00:00Z",
    "timeProvenance": "Declared",
    "network": "Forbidden",
    "locale": "invariant"
  },
  "budget": {
    "maxFiles": 1,
    "maxInputBytes": 1,
    "maxJsonDepth": 1,
    "componentTimeoutSeconds": 10,
    "totalTimeoutSeconds": 1
  }
}
"""
    Assert.True(ManifestParser.parse manifest |> Result.isError)
