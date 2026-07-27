module Tests

open Xunit
open CanonFlow.Assurance

[<Fact>]
let ``Digest extraction cannot mutate the constructed digest`` () =
    let callerOwned = Array.init 32 byte
    let digest =
        match Digest.create callerOwned with
        | Ok value -> value
        | Error error -> invalidOp error
    callerOwned.[0] <- 255uy
    let extracted = Digest.toBytes digest
    extracted.[1] <- 255uy
    let secondExtraction = Digest.toBytes digest
    Assert.Equal(0uy, secondExtraction.[0])
    Assert.Equal(1uy, secondExtraction.[1])
