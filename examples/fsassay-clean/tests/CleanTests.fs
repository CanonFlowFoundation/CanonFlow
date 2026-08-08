namespace FsassayClean.Tests

open Xunit

open Clean

type CleanTests() =
    [<Fact>]
    member _.``add returns the deterministic sum``() =
        Assert.Equal(5, add 2 3)
