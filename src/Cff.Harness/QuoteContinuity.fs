namespace Cff.Harness

open System
open System.Globalization
open System.Text.Json
open CanonFlow.Assurance.Contracts

type QuoteTotal = private QuoteTotal of decimal

[<RequireQualifiedAccess>]
module QuoteTotal =
    let create value =
        if value < 0M then Error "Quote total cannot be negative"
        else
            let scale = (Decimal.GetBits(value).[3] >>> 16) &&& 0x7F
            if scale > 2 then Error "Quote total exceeds the admitted two-decimal scale"
            else Ok (QuoteTotal value)
    let value (QuoteTotal value) = value

type Money =
    { Currency: string
      Amount: QuoteTotal }

type ParsedQuoteMessage =
    { Action: string
      Money: Money
      Evidence: MessageEvidence }

[<RequireQualifiedAccess>]
module QuoteContinuity =
    let ruleId = RuleId.create "ONDC-RET125-QUOTE-004" |> Result.defaultWith invalidOp
    let clauseId = ClauseId.create "RET125.flow3.quote-continuity" |> Result.defaultWith invalidOp
    let evaluatorId = EvaluatorId.create "quote-continuity/v1" |> Result.defaultWith invalidOp
    let profileId = ProfileId.create "ONDC-RET-B2C-1.2.5" |> Result.defaultWith invalidOp

    let private evidenceReference evidence =
        { EvidenceDigest = evidence.RawPayloadDigest
          JsonPath = Some "$.message.order.quote.price" }

    let parseMessage (evidence: MessageEvidence) =
        let reference = evidenceReference evidence
        let property (name: string) (element: JsonElement) =
            if element.ValueKind <> JsonValueKind.Object then None
            else
                element.EnumerateObject()
                |> Seq.tryFind (fun item -> item.NameEquals name)
                |> Option.map _.Value
        try
            use document = JsonDocument.Parse(ReadOnlyMemory<byte>(evidence.RawPayload))
            let root = document.RootElement
            let price =
                property "message" root
                |> Option.bind (property "order")
                |> Option.bind (property "quote")
                |> Option.bind (property "price")
            let amountElement = price |> Option.bind (property "value")
            let currencyElement = price |> Option.bind (property "currency")
            match amountElement, currencyElement with
            | Some amountElement, Some currencyElement
                when amountElement.ValueKind = JsonValueKind.String
                     && currencyElement.ValueKind = JsonValueKind.String ->
                let amountText = amountElement.GetString()
                let currency = currencyElement.GetString()
                let decimalStyles = NumberStyles.AllowLeadingSign ||| NumberStyles.AllowDecimalPoint
                match Decimal.TryParse(amountText, decimalStyles, CultureInfo.InvariantCulture) with
                | false, _ ->
                    Error (MalformedEvidence (reference, "Quote total is not an invariant non-exponent decimal"))
                | true, amount ->
                    match QuoteTotal.create amount with
                    | Error reason -> Error (MalformedEvidence (reference, reason))
                    | Ok quoteTotal when
                        isNull currency
                        || currency.Length <> 3
                        || currency |> Seq.exists (fun value -> value < 'A' || value > 'Z') ->
                        Error (MalformedEvidence (reference, "Currency must be a three-letter uppercase code"))
                    | Ok quoteTotal ->
                        Ok {
                            Action = evidence.Action
                            Money = { Currency = currency; Amount = quoteTotal }
                            Evidence = evidence
                        }
            | _ ->
                Error (MalformedEvidence (reference, "Quote price value and currency strings are required"))
        with
        | :? JsonException as error ->
            Error (MalformedEvidence (reference, "Malformed JSON: " + error.Message))
        | error ->
            Error (ParserToolFailure error.Message)

    let private missing requirement reason =
        Inconclusive (NonEmptyList.create { RequirementId = requirement; Reason = reason } [])

    let private allMessages bundle =
        bundle.Items
        |> List.choose (function Message message -> Some message | _ -> None)

    let evaluate bundle =
        let messages = allMessages bundle
        let exactlyOne action =
            messages
            |> List.filter (fun message -> String.Equals(message.Action, action, StringComparison.Ordinal))
            |> function
                | [ message ] -> Ok message
                | [] -> Error ($"Required {action} evidence is absent")
                | values -> Error ($"Multiple {action} messages are ambiguous ({values.Length})")
        match exactlyOne "on_select", exactlyOne "on_init" with
        | Error reason, _ | _, Error reason -> missing "quote-continuity-pair" reason
        | Ok selectEvidence, Ok initEvidence ->
            let correlationMatches =
                selectEvidence.Correlation.TransactionId = initEvidence.Correlation.TransactionId
                && selectEvidence.Correlation.SubscriberId = initEvidence.Correlation.SubscriberId
                && selectEvidence.Correlation.CounterpartyId = initEvidence.Correlation.CounterpartyId
                && selectEvidence.Correlation.MessageId <> initEvidence.Correlation.MessageId
            if not correlationMatches then
                missing "quote-continuity-correlation" "Transaction, participant, or message identity does not correlate"
            elif initEvidence.Timestamp < selectEvidence.Timestamp then
                missing "quote-continuity-order" "on_init precedes on_select"
            else
                match parseMessage selectEvidence, parseMessage initEvidence with
                | Error (ParserToolFailure error), _
                | _, Error (ParserToolFailure error) -> ToolFailure error
                | Error (MalformedEvidence (_, reason)), _
                | _, Error (MalformedEvidence (_, reason))
                | Error (UnsupportedEvidenceVersion reason), _
                | _, Error (UnsupportedEvidenceVersion reason) ->
                    missing "quote-total" reason
                | Ok select, Ok init when select.Money.Currency <> init.Money.Currency ->
                    let finding =
                        { RuleId = ruleId
                          Code = "QUOTE_CURRENCY_CHANGED"
                          Severity = Severity.Error
                          Message = "Quote currency changed between on_select and on_init"
                          Expected = Some select.Money.Currency
                          Observed = Some init.Money.Currency
                          Evidence =
                            [ { EvidenceDigest = selectEvidence.RawPayloadDigest
                                JsonPath = Some "$.message.order.quote.price.currency" }
                              { EvidenceDigest = initEvidence.RawPayloadDigest
                                JsonPath = Some "$.message.order.quote.price.currency" } ]
                          Authority = clauseId }
                    Violated (NonEmptyList.create finding [])
                | Ok select, Ok init when QuoteTotal.value select.Money.Amount = QuoteTotal.value init.Money.Amount ->
                    Satisfied
                | Ok select, Ok init ->
                    let display value = QuoteTotal.value value |> fun amount -> amount.ToString("0.00", CultureInfo.InvariantCulture)
                    let finding =
                        { RuleId = ruleId
                          Code = "QUOTE_TOTAL_CHANGED"
                          Severity = Severity.Error
                          Message = "Quote total changed between on_select and on_init"
                          Expected = Some (display select.Money.Amount)
                          Observed = Some (display init.Money.Amount)
                          Evidence =
                            [ { EvidenceDigest = selectEvidence.RawPayloadDigest
                                JsonPath = Some "$.message.order.quote.price.value" }
                              { EvidenceDigest = initEvidence.RawPayloadDigest
                                JsonPath = Some "$.message.order.quote.price.value" } ]
                          Authority = clauseId }
                    Violated (NonEmptyList.create finding [])
