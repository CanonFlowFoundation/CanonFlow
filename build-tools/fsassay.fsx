open System
open System.IO

let args = fsi.CommandLineArgs |> Array.tail
if args.Length = 0 then
    eprintfn "Usage: dotnet fsi fsassay.fsx <directory_to_scan>"
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
        if not (line.Contains("FsAssay-Ignore")) then
            let isComment = line.TrimStart().StartsWith("//")
            if not isComment then
                // Check for 'mutable ' keyword (with trailing space to avoid matching other things)
                if line.Contains("mutable ") || line.Contains(" mutable") then
                    eprintfn "%s(%d,1): error FSASSAY01: FsAssay violation: 'mutable' is forbidden. Use functional constructs." file (i + 1)
                    hasViolations <- true
                // Check for 'failwith'
                if line.Contains("failwith") then
                    eprintfn "%s(%d,1): error FSASSAY02: FsAssay violation: 'failwith' is forbidden. Use total functions (Result/Option)." file (i + 1)
                    hasViolations <- true
                // Check for 'interface ' (with trailing space)
                if line.Contains("interface ") || line.Contains(" interface") then
                    eprintfn "%s(%d,1): error FSASSAY03: FsAssay violation: OOP 'interface' is forbidden. Use Records of Functions." file (i + 1)
                    hasViolations <- true

getFiles targetDir
|> Seq.iter checkFile

if hasViolations then
    exit 1
else
    printfn "FsAssay: 0 violations found."
    exit 0
