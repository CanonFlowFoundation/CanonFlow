namespace Canon.Core.Tests

open System.IO
open Xunit
open FsCheck
open FsCheck.Xunit
open Canon.Core
open Canon.Introspect
open CanonFlow.Assurance
open CanonFlow.Profile.Pgsql.Experimental

module RequiredContactGenerators =
    let private text =
        Gen.elements [
            ""
            " "
            "a@example.test"
            "+91-555-0100"
            "unicode-\u03bb"
            " leading-preserved"
            "trailing-preserved "
        ]

    let validContact =
        Gen.oneof [
            text
            |> Gen.map (fun value ->
                value
                |> ContactText.create
                |> Result.map Contact.EmailOnly)
            text
            |> Gen.map (fun value ->
                value
                |> ContactText.create
                |> Result.map Contact.PhoneOnly)
            Gen.map2
                (fun email phone ->
                    match ContactText.create email, ContactText.create phone with
                    | Ok validEmail, Ok validPhone ->
                        Ok (Contact.Both (validEmail, validPhone))
                    | _ ->
                        Error ())
                text
                text
        ]
        |> Gen.map (function
            | Ok contact -> contact
            | Error () -> invalidOp "The closed laboratory text generator emitted whitespace.")

    let invalidDto =
        Gen.elements [
            { Email = None; Phone = None }
            { Email = Some null; Phone = None }
            { Email = None; Phone = Some null }
            { Email = Some null; Phone = Some "+91-555-0100" }
            { Email = Some "a@example.test"; Phone = Some null }
        ]

type RequiredContactArbitraries =
    static member Contact() =
        RequiredContactGenerators.validContact |> Arb.fromGen

module RequiredContactLabTests =
    let private recognized sql =
        match sql |> SqlParser.parseRowConstraint |> RequiredOrRecognizer.recognize with
        | RequiredOrRecognition.Recognized pattern -> pattern
        | result -> invalidOp $"Expected recognized required-OR, got {result}."

    [<Fact>]
    let ``Required OR row IR records both referenced columns`` () =
        let row =
            SqlParser.parseRowConstraint "(email IS NOT NULL OR phone IS NOT NULL)"
        Assert.True(
            Set.ofList ["email"; "phone"] = row.ReferencedColumns,
            $"Unexpected row IR: {row}")
        Assert.False(row.HasOpaqueNode, $"Unexpected row IR: {row}")
        match RequiredOrRecognizer.recognize row with
        | RequiredOrRecognition.Recognized pattern ->
            Assert.Equal(("email", "phone"), RequiredOrPattern.columns pattern)
        | result ->
            Assert.Fail($"Expected exact laboratory recognition, got {result}.")

    [<Fact>]
    let ``Opaque parser input is Inconclusive`` () =
        let row =
            SqlParser.parseRowConstraint "COALESCE(email, phone) IS NOT NULL"
        Assert.True(row.HasOpaqueNode)
        match RequiredOrRecognizer.recognize row with
        | RequiredOrRecognition.Inconclusive reasonId ->
            Assert.Equal("cff:reason:parser-uncertainty", reasonId)
        | result ->
            Assert.Fail($"Parser uncertainty was upgraded unexpectedly: {result}.")

    [<Fact>]
    let ``Positive negative and hostile recognizer corpus is classified`` () =
        let positive = [
            "email IS NOT NULL OR phone IS NOT NULL"
            "(phone IS NOT NULL OR email IS NOT NULL)"
        ]
        let negative = [
            "email IS NOT NULL AND phone IS NOT NULL"
            "email IS NULL OR phone IS NOT NULL"
            "email IS NOT NULL OR email IS NOT NULL"
            "email IS NOT NULL OR phone IS NOT NULL OR fax IS NOT NULL"
        ]
        let hostile = [
            "COALESCE(email, phone) IS NOT NULL"
            "email IS NOT NULL OR mystery(phone)"
            "email IS NOT NULL /* hidden OR phone IS NOT NULL */"
        ]
        for sql in positive do
            match sql |> SqlParser.parseRowConstraint |> RequiredOrRecognizer.recognize with
            | RequiredOrRecognition.Recognized _ -> ()
            | result -> Assert.Fail($"Positive corpus '{sql}' returned {result}.")
        for sql in negative do
            match sql |> SqlParser.parseRowConstraint |> RequiredOrRecognizer.recognize with
            | RequiredOrRecognition.Unsupported -> ()
            | result -> Assert.Fail($"Negative corpus '{sql}' returned {result}.")
        for sql in hostile do
            match sql |> SqlParser.parseRowConstraint |> RequiredOrRecognizer.recognize with
            | RequiredOrRecognition.Inconclusive _ -> ()
            | result -> Assert.Fail($"Hostile corpus '{sql}' returned {result}.")

    [<Property(Arbitrary = [| typeof<RequiredContactArbitraries> |], MaxTest = 200)>]
    let ``decode encode preserves every Contact`` (contact: Contact) =
        contact
        |> Contact.encode
        |> Contact.decode
        |> fun decoded -> decoded = Ok contact

    [<Fact>]
    let ``All three valid states decode and the absent state does not`` () =
        let valid = [
            { Email = Some "a@example.test"; Phone = None }, "EmailOnly"
            { Email = None; Phone = Some "+91-555-0100" }, "PhoneOnly"
            { Email = Some "a@example.test"; Phone = Some "+91-555-0100" }, "Both"
        ]
        for dto, expectedCase in valid do
            match Contact.decode dto with
            | Ok (Contact.EmailOnly _) -> Assert.Equal("EmailOnly", expectedCase)
            | Ok (Contact.PhoneOnly _) -> Assert.Equal("PhoneOnly", expectedCase)
            | Ok (Contact.Both _) -> Assert.Equal("Both", expectedCase)
            | Error error -> Assert.Fail($"Valid DTO failed with {error}.")
        Assert.Equal(
            Error ContactDecodeError.BothFieldsMissing,
            Contact.decode { Email = None; Phone = None })

    [<Fact>]
    let ``Invalid FsCheck generator produces only rejected DTOs`` () =
        RequiredContactGenerators.invalidDto
        |> Gen.sample 20 100
        |> List.iter (fun dto ->
            Assert.True(Contact.decode dto |> Result.isError))

    [<Fact>]
    let ``Generated FSharp output and evidence are deterministic`` () =
        let pattern =
            recognized "email IS NOT NULL OR phone IS NOT NULL"
        let first =
            RequiredContactEmitter.emitFSharpModule "RequiredContact.Generated" pattern
        let second =
            RequiredContactEmitter.emitFSharpModule "RequiredContact.Generated" pattern
        Assert.Equal(first, second)
        Assert.Equal(
            "sha256:e12e74dd9aa3e6adea74d6d614b8f87bdbf03fc148d71dac024b1cb29065b842",
            first |> Digest.sha256Text |> Digest.toString)
        let firstManifest =
            RequiredContactEvidence.manifest ()
            |> ObligationManifest.serialize
        let secondManifest =
            RequiredContactEvidence.manifest ()
            |> ObligationManifest.serialize
        Assert.Equal(firstManifest, secondManifest)
        Assert.Contains("\"state\":\"Admitted\"", firstManifest)
        let report = RequiredContactEvidence.fidelityReport ()
        Assert.Contains("- Status: Experimental", report)

        let repositoryRoot =
            Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "..", ".."))
        let read path =
            File.ReadAllText(Path.Combine(repositoryRoot, "examples", path))
                .Replace("\r\n", "\n")
        Assert.Equal(
            first,
            read "required-contact-lab-generated.fs")
        Assert.Equal(
            firstManifest,
            (read "required-contact-lab-obligation-manifest.json")
                .TrimEnd('\r', '\n'))
        Assert.Equal(
            report,
            read "required-contact-lab-fidelity.md")
        let corpus = read "required-contact-lab-corpus.json"
        Assert.Contains("\"positive\"", corpus)
        Assert.Contains("\"negative\"", corpus)
        Assert.Contains("\"hostile\"", corpus)
        let source =
            (read "required-contact-lab-source.sql")
                .TrimEnd('\r', '\n')
        Assert.Equal(RequiredContactEvidence.SourceSql, source)
        Assert.Equal(
            "sha256:8a71fd4510146dbd2bf2822eef5b7934bfef70612b3fa1ad97d69d5938c2bded",
            source |> Digest.sha256Text |> Digest.toString)
