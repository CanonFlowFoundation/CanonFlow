namespace CanonFlow.Assurance.Tests

open System
open Xunit
open FsCheck
open FsCheck.Xunit
open CanonFlow.Assurance
open CanonFlow.Assurance.Verification

module KernelTests =

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
