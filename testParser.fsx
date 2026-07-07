open System
open FParsec
open Canon.Core
open Canon.Introspect.SqlParser

let testParse (sql: string) =
    match run (ws >>. pExpr .>> eof) sql with
    | Success(result, _, _) -> printfn "Success: %A" result
    | Failure(err, _, _) -> printfn "Error: %s" err

testParse "((total_cost >= 0.00))"
testParse "total_cost >= 0.00"
testParse "end_time > start_time"
