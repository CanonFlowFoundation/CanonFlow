
#r "nuget: Argu, 6.2.5"
open Argu

type SubArgs =
    | Foo of string
    interface IArgParserTemplate with
        member s.Usage = "foo"

type MainArgs =
    | [<CliPrefix(CliPrefix.None)>] Sub of ParseResults<SubArgs>
    | Bar
    interface IArgParserTemplate with
        member s.Usage = "bar"

let parser = ArgumentParser.Create<MainArgs>()
let res = parser.ParseCommandLine([| "sub"; "--foo"; "test" |])

if res.Contains(Sub) then
    let sub = res.GetResult(Sub)
    printfn "%A" (sub.GetResult(Foo))

