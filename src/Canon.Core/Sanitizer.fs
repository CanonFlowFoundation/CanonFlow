namespace Canon.Core

open System
open System.Text.RegularExpressions

module Sanitizer =
    let sanitizeComment (text: string) =
        if String.IsNullOrEmpty(text) then text
        else
            text.Replace("/*", "/ *")
                .Replace("*/", "* /")
                .Replace("\n", " ")
                .Replace("\r", " ")
                .Replace("`", "'")

    let sanitizeIdentifier (name: string) =
        if String.IsNullOrEmpty(name) then "unnamed"
        else
            let maxLen = 100
            let truncated = if name.Length > maxLen then name.Substring(0, maxLen) else name
            // Allow alphanumeric and underscores. Anything else becomes underscore.
            let clean = Regex.Replace(truncated, @"[^a-zA-Z0-9_]", "_")
            // Ensure it starts with a letter or underscore
            if Char.IsDigit(clean.[0]) then "_" + clean
            else clean
