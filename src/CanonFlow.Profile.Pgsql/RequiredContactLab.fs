namespace CanonFlow.Profile.Pgsql.Experimental

open System
open System.Text
open Canon.Core
open CanonFlow.Assurance

type RequiredOrPattern =
    private RequiredOrPattern of leftColumn: string * rightColumn: string

[<RequireQualifiedAccess>]
module RequiredOrPattern =
    let columns (RequiredOrPattern (left, right)) = left, right

[<RequireQualifiedAccess>]
type RequiredOrRecognition =
    | Recognized of RequiredOrPattern
    | Inconclusive of reasonId: string
    | Unsupported

[<RequireQualifiedAccess>]
module RequiredOrRecognizer =
    let private isNotNull = function
        | Lattice.Leaf (FieldBound (column, Constraint.IsNotNull)) ->
            Some column
        | _ ->
            None

    let recognize (rowConstraint: RowConstraint) =
        if rowConstraint.HasOpaqueNode then
            RequiredOrRecognition.Inconclusive "cff:reason:parser-uncertainty"
        else
            match rowConstraint.Predicate with
            | Lattice.Or (left, right) ->
                match isNotNull left, isNotNull right with
                | Some leftColumn, Some rightColumn when leftColumn <> rightColumn ->
                    let first, second =
                        if String.CompareOrdinal(leftColumn, rightColumn) <= 0 then
                            leftColumn, rightColumn
                        else
                            rightColumn, leftColumn
                    let expectedColumns = Set.ofList [first; second]
                    if rowConstraint.ReferencedColumns = expectedColumns then
                        RequiredOrPattern (first, second)
                        |> RequiredOrRecognition.Recognized
                    else
                        RequiredOrRecognition.Inconclusive "cff:reason:reference-set-mismatch"
                | _ ->
                    RequiredOrRecognition.Unsupported
            | _ ->
                RequiredOrRecognition.Unsupported

type ContactText = private ContactText of string

[<RequireQualifiedAccess>]
module ContactText =
    let create value =
        if isNull value then
            Error ()
        else
            Ok (ContactText value)

    let value (ContactText value) = value

type Contact =
    | EmailOnly of email: ContactText
    | PhoneOnly of phone: ContactText
    | Both of email: ContactText * phone: ContactText

type ContactDto = {
    Email: string option
    Phone: string option
}

[<RequireQualifiedAccess>]
type ContactDecodeError =
    | BothFieldsMissing
    | InvalidEmail
    | InvalidPhone

[<RequireQualifiedAccess>]
module Contact =
    let encode = function
        | EmailOnly email ->
            {
                Email = Some (ContactText.value email)
                Phone = None
            }
        | PhoneOnly phone ->
            {
                Email = None
                Phone = Some (ContactText.value phone)
            }
        | Both (email, phone) ->
            {
                Email = Some (ContactText.value email)
                Phone = Some (ContactText.value phone)
            }

    let decode dto =
        match dto.Email, dto.Phone with
        | None, None ->
            Error ContactDecodeError.BothFieldsMissing
        | Some email, None ->
            email
            |> ContactText.create
            |> Result.map EmailOnly
            |> Result.mapError (fun () -> ContactDecodeError.InvalidEmail)
        | None, Some phone ->
            phone
            |> ContactText.create
            |> Result.map PhoneOnly
            |> Result.mapError (fun () -> ContactDecodeError.InvalidPhone)
        | Some email, Some phone ->
            match ContactText.create email, ContactText.create phone with
            | Ok validEmail, Ok validPhone ->
                Ok (Both (validEmail, validPhone))
            | Error (), _ ->
                Error ContactDecodeError.InvalidEmail
            | _, Error () ->
                Error ContactDecodeError.InvalidPhone

[<RequireQualifiedAccess>]
module RequiredContactEmitter =
    let emitFSharpModule moduleName pattern =
        let emailColumn, phoneColumn = RequiredOrPattern.columns pattern
        let output = StringBuilder()
        output.AppendLine($"module {moduleName}") |> ignore
        output.AppendLine("") |> ignore
        output.AppendLine($"// Experimental projection of: {emailColumn} IS NOT NULL OR {phoneColumn} IS NOT NULL") |> ignore
        output.AppendLine("type ContactText = private ContactText of string") |> ignore
        output.AppendLine("") |> ignore
        output.AppendLine("module ContactText =") |> ignore
        output.AppendLine("    let create value = if isNull value then Error () else Ok (ContactText value)") |> ignore
        output.AppendLine("    let value (ContactText value) = value") |> ignore
        output.AppendLine("") |> ignore
        output.AppendLine("type Contact =") |> ignore
        output.AppendLine("    | EmailOnly of email: ContactText") |> ignore
        output.AppendLine("    | PhoneOnly of phone: ContactText") |> ignore
        output.AppendLine("    | Both of email: ContactText * phone: ContactText") |> ignore
        output.AppendLine("") |> ignore
        output.AppendLine("type ContactDto = { Email: string option; Phone: string option }") |> ignore
        output.AppendLine("") |> ignore
        output.AppendLine("let encode = function") |> ignore
        output.AppendLine("    | EmailOnly email -> { Email = Some (ContactText.value email); Phone = None }") |> ignore
        output.AppendLine("    | PhoneOnly phone -> { Email = None; Phone = Some (ContactText.value phone) }") |> ignore
        output.AppendLine("    | Both (email, phone) -> { Email = Some (ContactText.value email); Phone = Some (ContactText.value phone) }") |> ignore
        output.AppendLine("") |> ignore
        output.AppendLine("let decode dto =") |> ignore
        output.AppendLine("    match dto.Email, dto.Phone with") |> ignore
        output.AppendLine("    | None, None -> Error \"both-fields-missing\"") |> ignore
        output.AppendLine("    | Some email, None -> ContactText.create email |> Result.map EmailOnly |> Result.mapError (fun () -> \"null-field\")") |> ignore
        output.AppendLine("    | None, Some phone -> ContactText.create phone |> Result.map PhoneOnly |> Result.mapError (fun () -> \"null-field\")") |> ignore
        output.AppendLine("    | Some email, Some phone ->") |> ignore
        output.AppendLine("        match ContactText.create email, ContactText.create phone with") |> ignore
        output.AppendLine("        | Ok validEmail, Ok validPhone -> Ok (Both (validEmail, validPhone))") |> ignore
        output.AppendLine("        | Error (), Ok _ -> Error \"null-field\"") |> ignore
        output.AppendLine("        | Ok _, Error () -> Error \"null-field\"") |> ignore
        output.AppendLine("        | Error (), Error () -> Error \"null-field\"") |> ignore
        output.ToString().Replace("\r\n", "\n")

[<RequireQualifiedAccess>]
module RequiredContactEvidence =
    [<Literal>]
    let SourceSql = "CHECK (email IS NOT NULL OR phone IS NOT NULL)"

    [<Literal>]
    let NormalizedPredicate = "or(is-not-null(email),is-not-null(phone))"

    let private identifier create value =
        match create value with
        | Ok identifier -> identifier
        | Error error -> invalidOp error

    let manifest () =
        let gate gateId implementation =
            ProofGateReference.create
                (identifier ProofGateId.create gateId)
                "v1"
                (Digest.sha256Text implementation)
            |> function
                | Ok reference -> reference
                | Error error -> invalidOp error
        let obligation =
            Obligation.create
                (identifier ObligationId.create "cff:lab:required-contact")
                (Digest.sha256Text SourceSql)
                (Digest.sha256Text NormalizedPredicate)
                [
                    gate "cff:gate:postgres-truth-table" "postgres-truth-table:v1"
                    gate "cff:gate:round-trip" "contact-round-trip:v1"
                    gate "cff:gate:parser-certainty" "required-or-recognizer:v1"
                    gate "cff:gate:mutation-corpus" "required-contact-mutations:v1"
                ]
                (ProjectionDerivation.Admitted (
                    identifier AdmissionId.create "cff:admission:cm2-required-contact-lab"
                ))
            |> function
                | Ok value -> value
                | Error error -> invalidOp error
        ObligationManifest.create
            (Digest.sha256Text "cm2:required-contact:policy:v1")
            [obligation]
        |> function
            | Ok value -> value
            | Error error -> invalidOp error

    let fidelityReport () =
        let manifest = manifest ()
        String.concat "\n" [
            "# Required Contact Laboratory Fidelity Report"
            ""
            "- Status: Experimental"
            "- Claim: ConstructivelyProjected"
            $"- Scope: `{SourceSql}`"
            $"- Source digest: `{Digest.sha256Text SourceSql |> Digest.toString}`"
            $"- Predicate digest: `{Digest.sha256Text NormalizedPredicate |> Digest.toString}`"
            $"- Manifest protected digest: `{ObligationManifest.protectedDigest manifest |> Digest.toString}`"
            "- Admitted states: EmailOnly, PhoneOnly, Both"
            "- Excluded state: both fields absent"
            "- Oracle: four-row PostgreSQL truth table"
            "- Limits: row-local laboratory pattern only; no regulatory authority"
            ""
        ]
