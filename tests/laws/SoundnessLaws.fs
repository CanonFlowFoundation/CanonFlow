namespace Canon.Core.Tests

open Xunit
open Canon.Core
open Canon.Fable

module SoundnessLaws =

    [<Fact>]
    let ``Soundness Law: if database admits NULL, TS validator admits NULL`` () =
        // Specimen S1: guarantor_share_pct >= 10, but column is nullable
        // Database admits NULL.
        // We ensure Transpiler generates a guard that returns true for null.
        let lattice = Lattice.Leaf (Range(Some(Inclusive 10m), None))
        let tsCode, fidelity = Transpiler.emitValidator "guarantor_share_pct" lattice true None
        
        // Assert the code has the null guard
        Assert.Contains(".nullable()", tsCode)

    [<Fact>]
    let ``Soundness Law: if database admits NULL, Kotlin validator admits NULL`` () =
        let lattice = Lattice.Leaf (Range(Some(Inclusive 10m), None))
        let ktCode, fidelity = KotlinTranspiler.emitValidator "guarantor_share_pct" lattice true None
        Assert.Contains("if (value == null) return true", ktCode)

    [<Fact>]
    let ``Soundness Law: if database admits NULL, Swift validator admits NULL`` () =
        let lattice = Lattice.Leaf (Range(Some(Inclusive 10m), None))
        let swCode, fidelity = SwiftTranspiler.emitValidator "guarantor_share_pct" lattice true None
        Assert.Contains("if value == nil { return true }", swCode)
