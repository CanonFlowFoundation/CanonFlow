namespace Cff.Harness.Tests

open System
open Xunit
open CanonFlow.Assurance.Contracts

type ContractsBoundaryTests() =
    [<Fact>]
    member _.``identifiers require validated construction`` () =
        Assert.True(RuleId.create "" |> Result.isError)
        Assert.True(ClauseId.create " " |> Result.isError)
        Assert.True(ProfileId.create null |> Result.isError)
        Assert.True(EvaluatorId.create "quote/v1" |> Result.isOk)

    [<Fact>]
    member _.``content digest rejects noncanonical input`` () =
        Assert.True(ContentDigest.createSha256 "SHA256:ABC" |> Result.isError)
        Assert.True(ContentDigest.createSha256 "sha256:00" |> Result.isError)
        Assert.True(
            ContentDigest.createSha256 ("sha256:" + String.replicate 64 "0")
            |> Result.isOk
        )

    [<Fact>]
    member _.``contracts assembly has no ONDCFlow dependency`` () =
        let references =
            typeof<RuleId>.Assembly.GetReferencedAssemblies()
            |> Array.map _.Name
        Assert.DoesNotContain(references, fun name ->
            name.StartsWith("ONDCFlow", StringComparison.Ordinal))

    [<Fact>]
    member _.``contracts assembly has no infrastructure dependency`` () =
        let references =
            typeof<RuleId>.Assembly.GetReferencedAssemblies()
            |> Array.map _.Name
            |> Set.ofArray
        Assert.DoesNotContain("System.Net.Http", references)
        Assert.DoesNotContain("System.IO.FileSystem", references)
        Assert.DoesNotContain("BouncyCastle.Cryptography", references)

    [<Fact>]
    member _.``public API contains generic observation evidence`` () =
        let payload = [| 1uy |]
        let digest =
            ContentDigest.createSha256 ("sha256:" + String.replicate 64 "0")
            |> Result.defaultWith invalidOp
        let provenance =
            { CapturedBy = "runner"
              CapturedAt = DateTimeOffset.UnixEpoch
              CaptureMethod = ExternalToolImport
              Producer = None
              AttestationDigest = None
              EstablishedTrust = IndependentlyCaptured }
        let evidence =
            Observation {
                Kind = ReplayObservation
                Payload = payload
                ObservationDigest = digest
                Provenance = provenance
            }
        match evidence with
        | Observation observation -> Assert.Equal(ReplayObservation, observation.Kind)
        | _ -> Assert.Fail("Expected observation evidence")
