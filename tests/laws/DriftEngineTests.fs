namespace CanonFlow.Tests

open Xunit
open FsCheck
open FsCheck.Xunit
open Canon.Core
open Canon.Core.DriftEngine

module DriftEngineTests =

    let buildBound (min: decimal option) (max: decimal option) =
        let minBound = min |> Option.map (fun m -> Inclusive m)
        let maxBound = max |> Option.map (fun m -> Inclusive m)
        Leaf (FieldBound ("x", Range (minBound, maxBound)))

    [<Fact>]
    let ``DriftEngine: Exact identical constraints return Aligned`` () =
        let source = buildBound (Some 0M) (Some 100M)
        let target = buildBound (Some 0M) (Some 100M)
        let result = calculateSemanticDrift source target
        Assert.Equal(SemanticDriftStatus.Aligned, result)

    [<Fact>]
    let ``DriftEngine: Target with tighter bounds returns StrictTarget`` () =
        let source = buildBound (Some 0M) (Some 100M)
        let target = buildBound (Some 10M) (Some 50M)
        let result = calculateSemanticDrift source target
        Assert.Equal(SemanticDriftStatus.StrictTarget, result)

    [<Fact>]
    let ``DriftEngine: Target with looser bounds returns LooseTarget`` () =
        let source = buildBound (Some 10M) (Some 50M)
        let target = buildBound (Some 0M) (Some 100M)
        let result = calculateSemanticDrift source target
        Assert.Equal(SemanticDriftStatus.LooseTarget, result)

    [<Fact>]
    let ``DriftEngine: Disjoint bounds return Disjoint`` () =
        let source = buildBound (Some 0M) (Some 10M)
        let target = buildBound (Some 20M) (Some 30M)
        let result = calculateSemanticDrift source target
        Assert.Equal(SemanticDriftStatus.Disjoint, result)

    [<Fact>]
    let ``DriftEngine: Overlapping but uncontained bounds return Disjoint`` () =
        let source = buildBound (Some 0M) (Some 50M)
        let target = buildBound (Some 25M) (Some 75M)
        let result = calculateSemanticDrift source target
        // They intersect, but neither is a subset of the other.
        Assert.Equal(SemanticDriftStatus.Disjoint, result)
