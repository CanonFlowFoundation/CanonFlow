open System
open FParsec

let pDecimal = 
    let pNum = many1Chars (digit)
    let pFrac = pchar '.' >>. pNum |>> fun s -> "." + s
    let pSign = opt (pchar '-') |>> function Some _ -> "-" | None -> ""
    pipe3 pSign pNum (opt pFrac) (fun s n f -> 
        match f with
        | Some frac -> decimal (s + n + frac)
        | None -> decimal (s + n)
    )

match run (pDecimal .>> eof) "0.00" with
| Success(r, _, _) -> printfn "Success: %M" r
| Failure(e, _, _) -> printfn "Error: %s" e
