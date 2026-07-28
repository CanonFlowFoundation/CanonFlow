open System
open System.IO

let args = fsi.CommandLineArgs |> Array.tail
if args.Length = 0 then
    eprintfn "Usage: dotnet fsi canonflow-lexical-guard.fsx <directory_to_scan>"
    exit 1

let targetDir = args.[0]

if not (Directory.Exists(targetDir)) then
    eprintfn "Directory does not exist: %s" targetDir
    exit 1

let rec getFiles dir =
    seq {
        let name = Path.GetFileName(dir : string)
        if name <> "bin" && name <> "obj" && name <> ".fable" then
            yield! Directory.EnumerateFiles(dir, "*.fs")
            yield! Directory.EnumerateFiles(dir, "*.fsi")
            for subDir in Directory.EnumerateDirectories(dir) do
                yield! getFiles subDir
    }

let mutable hasViolations = false

let checkFile file =
    let lines = File.ReadAllLines(file)
    for i in 0 .. lines.Length - 1 do
        let line = lines.[i]
        if not (
            line.Contains("CanonFlow-Lexical-Ignore")
            || line.Contains("FsAssay-Ignore")) then
            let isComment = line.TrimStart().StartsWith("//")
            if not isComment then
                // Check for 'mutable ' keyword (with trailing space to avoid matching other things)
                if line.Contains("mutable ") || line.Contains(" mutable") then
                    eprintfn "%s(%d,1): error CFLEX01: CanonFlow lexical guard: 'mutable' is forbidden. Use functional constructs." file (i + 1)
                    hasViolations <- true
                // Check for 'failwith'
                if line.Contains("failwith") then
                    eprintfn "%s(%d,1): error CFLEX02: CanonFlow lexical guard: 'failwith' is forbidden. Use total functions (Result/Option)." file (i + 1)
                    hasViolations <- true
                // Check for 'interface ' (with trailing space)
                if line.Contains("interface ") || line.Contains(" interface") then
                    eprintfn "%s(%d,1): error CFLEX03: CanonFlow lexical guard: OOP 'interface' is forbidden. Use Records of Functions." file (i + 1)
                    hasViolations <- true

getFiles targetDir
|> Seq.iter checkFile

if hasViolations then
    exit 1
else
    printfn "CanonFlow lexical guard: 0 violations found."
    exit 0
