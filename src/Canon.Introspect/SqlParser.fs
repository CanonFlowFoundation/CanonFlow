namespace Canon.Introspect

open FParsec
open Canon.Core

module SqlParser =

    let ws = spaces

    let pIdentifier = many1Chars (asciiLetter <|> digit <|> pchar '_')

    let pDecimal = 
        let pNum = many1Chars (digit)
        let pFrac = pchar '.' >>. pNum |>> fun s -> "." + s
        pipe2 pNum (opt pFrac) (fun n f -> 
            match f with
            | Some frac -> decimal (n + frac)
            | None -> decimal n
        )

    let pGreaterThan = pstring ">" >>. ws >>. pDecimal |>> fun num -> Range(Some(Exclusive num), None)
    let pGreaterThanOrEqual = pstring ">=" >>. ws >>. pDecimal |>> fun num -> Range(Some(Inclusive num), None)
    let pLessThan = pstring "<" >>. ws >>. pDecimal |>> fun num -> Range(None, Some(Exclusive num))
    let pLessThanOrEqual = pstring "<=" >>. ws >>. pDecimal |>> fun num -> Range(None, Some(Inclusive num))

    let pOp = choice [ attempt pGreaterThanOrEqual; pGreaterThan; attempt pLessThanOrEqual; pLessThan ]

    let pCondition = 
        pipe2 (pIdentifier .>> ws) pOp (fun ident op -> Lattice.Leaf (FieldBound(ident, op)))

    let opp = new OperatorPrecedenceParser<Lattice<Constraint>, unit, unit>()
    let pExpr = opp.ExpressionParser
    opp.TermParser <- pCondition <|> between (pstring "(" >>. ws) (pstring ")" >>. ws) pExpr

    opp.AddOperator(InfixOperator("AND", ws, 2, Associativity.Left, Lattice.and'))
    opp.AddOperator(InfixOperator("OR", ws, 1, Associativity.Left, Lattice.or'))
    opp.AddOperator(PrefixOperator("NOT", ws, 3, true, Lattice.not))

    let parseConstraint (sql: string) =
        match run (ws >>. pExpr .>> eof) sql with
        | Success(result, _, _) -> result
        | Failure(_, _, _) -> Lattice.Leaf(Opaque sql)
