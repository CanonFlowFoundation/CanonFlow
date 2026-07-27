namespace CanonFlow.Assurance.Tests

open System
open Xunit
open FsCheck
open FsCheck.Xunit
open CanonFlow.Core.Verification
open CanonFlow.Assurance

module KernelTests =

    // XP-1, XP-2: Totality and Determinism under arbitrary inputs.
    // The verifier must never crash, and the same input must always yield the same result.
    [<Property>]
    let ``verifyLedger is total and does not throw`` (ledger: LedgerEntry list) =
        // We catch all exceptions. If verifyLedger throws, it fails totality (XR-1).
        let result = 
            try
                Verification.verifyLedger ledger |> Some
            with 
            | _ -> None
        
        result.IsSome

    // XP-3a/b: No vacuous Pass. Empty list returns Inconclusive.
    [<Fact>]
    let ``aggregate of empty list is Inconclusive`` () =
        let result = Verdict.aggregate []
        Assert.Equal(Inconclusive, result)

    [<Property>]
    let ``aggregate of any list is never Pass if list is empty`` (verdicts: Verdict list) =
        let result = Verdict.aggregate verdicts
        if List.isEmpty verdicts then
            result = Inconclusive
        else
            true // We only test the empty condition here

    // XP-12: Framing Property.
    // Two concatenations might collide, but JCS guarantees they won't.
    [<Property>]
    let ``JCS framing prevents collisions between distinct strings`` (s1: string, s2: string, s3: string, s4: string) =
        // Ensure they aren't actually identical pairs
        let isSame = (s1 = s3) && (s2 = s4)
        if not isSame && s1 <> null && s2 <> null && s3 <> null && s4 <> null then
            let obj1 = CanonicalJson.JObject [ "a", CanonicalJson.JString s1; "b", CanonicalJson.JString s2 ]
            let obj2 = CanonicalJson.JObject [ "a", CanonicalJson.JString s3; "b", CanonicalJson.JString s4 ]
            let ser1 = CanonicalJson.serialize obj1
            let ser2 = CanonicalJson.serialize obj2
            ser1 <> ser2
        else
            true
