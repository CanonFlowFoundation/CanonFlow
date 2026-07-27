namespace CanonFlow.Assurance

open System
open System.Security.Cryptography
open System.Text

module Hash =
    let computeSha256 (input: string) =
#if !FABLE_COMPILER
        use sha256 = SHA256.Create()
        let bytes = Encoding.UTF8.GetBytes(input)
        let hashBytes = sha256.ComputeHash(bytes)
        BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant()
#else
        "wasm-hash-pending"
#endif

