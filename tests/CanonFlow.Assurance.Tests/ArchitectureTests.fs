namespace CanonFlow.Assurance.Tests

open System
open Xunit
open CanonFlow.Assurance

type ArchitectureTests() =
    [<Fact>]
    member _.``assurance kernel has no ONDCFlow dependency`` () =
        let references =
            typeof<Digest>.Assembly.GetReferencedAssemblies()
            |> Array.map _.Name

        Assert.DoesNotContain(
            references,
            fun name -> name.StartsWith("ONDCFlow", StringComparison.Ordinal)
        )
