namespace CanonFlow.Assurance.Tests

open System
open Xunit
open FsCheck
open FsCheck.Xunit
open CanonFlow.Assurance
open CanonFlow.Assurance.Verification
open CanonFlow.Assurance.Signing
open System.Text.Json
open System.Text.Json.Nodes

module KernelTests =

    let private sampleReceipt verdict : CanonFlowEvidenceReceiptV11 =
        {
            SchemaVersion = "1.1"
            ReceiptType = "CanonFlowEvidenceReceipt"
            ReplayIdentity = "sha256:9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08"
            Subject = {
                Root = "fixture"
                Schema = "test"
                SourceDirectories = ["src"]
                ManifestDigest = Some "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                Artifacts = [{
                    Path = "test"
                    Digest = "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
                }]
            }
            Evaluator = { EngineId = "test"; EngineVersion = "0.1.1-alpha" }
            Context = { Instant = "2026-07-27T00:00:00Z"; TimeProvenance = "Declared"; Locale = "invariant"; NetworkPolicy = "Forbidden" }
            Assessments = [{
                ComponentId = "fixture"
                ComponentVersion = "1"
                Health = EvidenceHealth.Complete
                Compliance = Compliance.Conformant
                ApplicableRules = 1
                EvaluatedRules = 1
                Evidence = []
            }]
            ConstructiveAssessments = []
            Verdict = verdict
            Seal = None
        }

    // XP-1, XP-2: Totality and Determinism under arbitrary inputs.


    // Exhaustive Semilattice properties for Verdict join operator
    // There are 4 states: Pass, Fail, Inconclusive, ToolFailure
    let allVerdicts = [ Verdict.Pass; Verdict.Fail; Verdict.Inconclusive; Verdict.ToolFailure ]

    [<Fact>]
    let ``Verdict join is associative (4^3 = 64 cases)`` () =
        for a in allVerdicts do
            for b in allVerdicts do
                for c in allVerdicts do
                    let left = Verdict.join (Verdict.join a b) c
                    let right = Verdict.join a (Verdict.join b c)
                    Assert.Equal(left, right)

    [<Fact>]
    let ``Verdict join is commutative (4^2 = 16 cases)`` () =
        for a in allVerdicts do
            for b in allVerdicts do
                Assert.Equal(Verdict.join a b, Verdict.join b a)

    [<Fact>]
    let ``Verdict join is idempotent (4 cases)`` () =
        for a in allVerdicts do
            Assert.Equal(Verdict.join a a, a)

    // XP-3a/b: No vacuous Pass. Empty list returns Inconclusive.
    [<Fact>]
    let ``Assessment summarize of empty list with Complete health is Inconclusive`` () =
        let result = Assessment.summarize EvidenceHealth.Complete []
        Assert.Equal(Verdict.Inconclusive, result)

    [<Fact>]
    let ``Assessment summarize of empty list with Broken health is ToolFailure`` () =
        let result = Assessment.summarize (EvidenceHealth.Broken {Description="err"}) []
        Assert.Equal(Verdict.ToolFailure, result)

    [<Fact>]
    let ``Constructive modelling is dormant without weakening verification`` () =
        Assert.Equal(ConstructiveMode.Dormant, ClaimPolicy.defaultConstructiveMode)
        Assert.False(ClaimPolicy.canEmitConstructiveProjection ClaimPolicy.defaultConstructiveMode)
        let verdict =
            Assessment.summarize
                EvidenceHealth.Complete
                [ Truth.Clear ClearOutcome.Conformant ]
        Assert.Equal(Verdict.Pass, verdict)

    [<Fact>]
    let ``Pass envelope with no assessments is rejected`` () =
        let receipt =
            { sampleReceipt Verdict.Pass with Assessments = [] }
            |> CanonicalReceiptJson.serializeReceipt
        Assert.True(
            ReceiptVerifier.verifyEnvelopeJson receipt None true
            |> Result.isError)

    [<Fact>]
    let ``Pass envelope with zero applicable rules is rejected`` () =
        let receipt =
            {
                sampleReceipt Verdict.Pass with
                    Assessments =
                        [{
                            (sampleReceipt Verdict.Pass).Assessments.Head with
                                ApplicableRules = 0
                                EvaluatedRules = 0
                        }]
            }
            |> CanonicalReceiptJson.serializeReceipt
        Assert.True(
            ReceiptVerifier.verifyEnvelopeJson receipt None true
            |> Result.isError)

    [<Fact>]
    let ``Signed receipt verifies and a signed-field mutation fails`` () =
        let seed = Convert.FromHexString("9d61b19deffd5a60ba844af492ec2cc44449c5697b326919703bac031cae7f60")
        let publicKey = Convert.FromHexString("d75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a")
        let privateKey =
            match PrivateKey.create seed with
            | Ok value -> value
            | Error error -> invalidOp error
        let signed = ReceiptVerifier.signReceipt "rfc8032:test-1" privateKey (sampleReceipt Verdict.Pass)
        Assert.True(ReceiptVerifier.verifyReceipt publicKey signed |> Result.isOk)
        let tampered = { signed with Verdict = Verdict.Fail }
        Assert.True(ReceiptVerifier.verifyReceipt publicKey tampered |> Result.isError)

    [<Fact>]
    let ``Identical receipts have byte-identical canonical payloads`` () =
        let first = sampleReceipt Verdict.Inconclusive |> CanonicalReceiptJson.serializeReceipt
        let second = sampleReceipt Verdict.Inconclusive |> CanonicalReceiptJson.serializeReceipt
        Assert.Equal(first, second)

    [<Fact>]
    let ``Canonical verifier rejects duplicate properties and noncanonical ordering`` () =
        Assert.True(ReceiptVerifier.verifyCanonicalJson """{"a":1,"a":1}""" |> Result.isError)
        Assert.True(ReceiptVerifier.verifyCanonicalJson """{"b":1,"a":1}""" |> Result.isError)
        Assert.True(ReceiptVerifier.verifyCanonicalJson """{"a":1,"b":1}""" |> Result.isOk)

    [<Fact>]
    let ``Envelope verifier rejects canonical JSON that is not a receipt`` () =
        Assert.True(
            ReceiptVerifier.verifyEnvelopeJson """{"seal":null}""" None true
            |> Result.isError)

    [<Fact>]
    let ``Every serialized receipt leaf mutation invalidates the signed envelope`` () =
        let seed = Convert.FromHexString("9d61b19deffd5a60ba844af492ec2cc44449c5697b326919703bac031cae7f60")
        let publicKey = Convert.FromHexString("d75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a")
        let privateKey =
            match PrivateKey.create seed with
            | Ok value -> value
            | Error error -> invalidOp error
        let signed = ReceiptVerifier.signReceipt "rfc8032:test-1" privateKey (sampleReceipt Verdict.Pass)
        let envelope = CanonicalReceiptJson.serializeEnvelope signed

        let rec mutations (node: JsonNode) =
            seq {
                match node with
                | :? JsonObject as objectNode ->
                    for property in objectNode do
                        if not (isNull property.Value) then
                            for mutation in mutations property.Value do
                                let clone = objectNode.DeepClone().AsObject()
                                clone.[property.Key] <- mutation
                                yield clone :> JsonNode
                        else
                            let clone = objectNode.DeepClone().AsObject()
                            clone.[property.Key] <- JsonValue.Create("tampered")
                            yield clone :> JsonNode
                | :? JsonArray as arrayNode ->
                    for index in 0 .. arrayNode.Count - 1 do
                        if not (isNull arrayNode.[index]) then
                            for mutation in mutations arrayNode.[index] do
                                let clone = arrayNode.DeepClone().AsArray()
                                clone.[index] <- mutation
                                yield clone :> JsonNode
                | _ ->
                    match node.GetValueKind() with
                    | JsonValueKind.String -> yield JsonValue.Create(node.GetValue<string>() + "-tampered")
                    | JsonValueKind.Number -> yield JsonValue.Create(node.GetValue<int>() + 1)
                    | JsonValueKind.True -> yield JsonValue.Create(false)
                    | JsonValueKind.False -> yield JsonValue.Create(true)
                    | JsonValueKind.Null -> yield JsonValue.Create("tampered")
                    | _ -> ()
            }

        let root = JsonNode.Parse(envelope)
        let mutated = mutations root |> Seq.toList
        Assert.NotEmpty(mutated)
        for mutation in mutated do
            let json = mutation.ToJsonString(JsonSerializerOptions(WriteIndented = false))
            Assert.True(
                ReceiptVerifier.verifyEnvelopeJson json (Some publicKey) false |> Result.isError,
                $"Mutation unexpectedly verified: {json}")

    // XP-12: Framing Property.
    // Two concatenations might collide, but JCS guarantees they won't.
    [<Property>]
    let ``JCS framing prevents collisions between distinct strings`` (s1: string, s2: string, s3: string, s4: string) =
        // Ensure they aren't actually identical pairs
        let isSame = (s1 = s3) && (s2 = s4)
        if not isSame && s1 <> null && s2 <> null && s3 <> null && s4 <> null then
            let obj1 = CanonicalJson.JObject [ "a", CanonicalJson.JString s1; "b", CanonicalJson.JString s2 ]
            let obj2 = CanonicalJson.JObject [ "a", CanonicalJson.JString s3; "b", CanonicalJson.JString s4 ]
            let ser1 = CanonicalReceiptJson.serialize obj1
            let ser2 = CanonicalReceiptJson.serialize obj2
            ser1 <> ser2
        else
            true
