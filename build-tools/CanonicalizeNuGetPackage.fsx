open System
open System.IO
open System.IO.Compression
open System.Text
open System.Text.RegularExpressions

let canonicalTimestamp = DateTimeOffset(2026, 7, 29, 0, 0, 0, TimeSpan.Zero)
let canonicalCorePropertiesPath =
    "package/services/metadata/core-properties/core.psmdcp"

let normalizeEntryName (name: string) =
    if name.StartsWith("package/services/metadata/core-properties/", StringComparison.Ordinal)
       && name.EndsWith(".psmdcp", StringComparison.Ordinal) then
        canonicalCorePropertiesPath
    else
        name

let normalizeEntryContent (name: string) (content: byte array) =
    if name = "_rels/.rels" then
        content
        |> Encoding.UTF8.GetString
        |> fun text ->
            Regex.Replace(
                text,
                "package/services/metadata/core-properties/[0-9a-fA-F]+\\.psmdcp",
                canonicalCorePropertiesPath,
                RegexOptions.CultureInvariant
            )
        |> fun text ->
            Regex.Replace(
                text,
                "(<Relationship Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\"[^>]* Id=\")[^\"]+(\")",
                "$1RCOREPROPERTIES$2",
                RegexOptions.CultureInvariant
            )
        |> Encoding.UTF8.GetBytes
    else
        content

let canonicalize (packagePath: string) =
    let entries =
        use input = ZipFile.OpenRead(packagePath)

        input.Entries
        |> Seq.map (fun entry ->
            use stream = entry.Open()
            use buffer = new MemoryStream()
            stream.CopyTo(buffer)
            let name = normalizeEntryName entry.FullName
            name, normalizeEntryContent entry.FullName (buffer.ToArray()))
        |> Seq.sortBy fst
        |> Seq.toArray

    let temporaryPath = packagePath + ".canonical"

    do
        use outputStream = File.Open(temporaryPath, FileMode.CreateNew, FileAccess.Write)
        use output = new ZipArchive(outputStream, ZipArchiveMode.Create, false)

        for name, content in entries do
            let entry = output.CreateEntry(name, CompressionLevel.Optimal)
            entry.LastWriteTime <- canonicalTimestamp
            use stream = entry.Open()
            stream.Write(content, 0, content.Length)

    File.Move(temporaryPath, packagePath, true)

match fsi.CommandLineArgs |> Array.skip 1 with
| [| packagePath |] when File.Exists packagePath -> canonicalize packagePath
| [| packagePath |] -> failwith $"Package does not exist: {packagePath}"
| _ -> failwith "Usage: dotnet fsi CanonicalizeNuGetPackage.fsx <package.nupkg>"
