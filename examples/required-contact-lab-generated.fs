module RequiredContact.Generated

// Experimental projection of: email IS NOT NULL OR phone IS NOT NULL
type ContactText = private ContactText of string

module ContactText =
    let create value = if isNull value then Error () else Ok (ContactText value)
    let value (ContactText value) = value

type Contact =
    | EmailOnly of email: ContactText
    | PhoneOnly of phone: ContactText
    | Both of email: ContactText * phone: ContactText

type ContactDto = { Email: string option; Phone: string option }

let encode = function
    | EmailOnly email -> { Email = Some (ContactText.value email); Phone = None }
    | PhoneOnly phone -> { Email = None; Phone = Some (ContactText.value phone) }
    | Both (email, phone) -> { Email = Some (ContactText.value email); Phone = Some (ContactText.value phone) }

let decode dto =
    match dto.Email, dto.Phone with
    | None, None -> Error "both-fields-missing"
    | Some email, None -> ContactText.create email |> Result.map EmailOnly |> Result.mapError (fun () -> "null-field")
    | None, Some phone -> ContactText.create phone |> Result.map PhoneOnly |> Result.mapError (fun () -> "null-field")
    | Some email, Some phone ->
        match ContactText.create email, ContactText.create phone with
        | Ok validEmail, Ok validPhone -> Ok (Both (validEmail, validPhone))
        | Error (), Ok _ -> Error "null-field"
        | Ok _, Error () -> Error "null-field"
        | Error (), Error () -> Error "null-field"
