module Tests

open System
open Xunit
open CanonFlow.Assurance.Xp

[<Fact>]
let ``XP Library can be loaded and prints correctly`` () =
    // Since it's a stub, we just ensure it executes without crashing
    Say.hello "CanonFlow"
