namespace CanonFlow.Assurance

open System
open System.Text

module Hash =

    // Pure F# SHA256 implementation
    let inline ror32 (value: uint32) (amount: int) : uint32 =
        (value >>> amount) ||| (value <<< (32 - amount))

    let K = 
        [|
            0x428a2f98u; 0x71374491u; 0xb5c0fbcfu; 0xe9b5dba5u; 0x3956c25bu; 0x59f111f1u; 0x923f82a4u; 0xab1c5ed5u
            0xd807aa98u; 0x12835b01u; 0x243185beu; 0x550c7dc3u; 0x72be5d74u; 0x80deb1feu; 0x9bdc06a7u; 0xc19bf174u
            0xe49b69c1u; 0xefbe4786u; 0x0fc19dc6u; 0x240ca1ccu; 0x2de92c6fu; 0x4a7484aau; 0x5cb0a9dcu; 0x76f988dau
            0x983e5152u; 0xa831c66du; 0xb00327c8u; 0xbf597fc7u; 0xc6e00bf3u; 0xd5a79147u; 0x06ca6351u; 0x14292967u
            0x27b70a85u; 0x2e1b2138u; 0x4d2c6dfcu; 0x53380d13u; 0x650a7354u; 0x766a0abbu; 0x81c2c92eu; 0x92722c85u
            0xa2bfe8a1u; 0xa81a664bu; 0xc24b8b70u; 0xc76c51a3u; 0xd192e819u; 0xd6990624u; 0xf40e3585u; 0x106aa070u
            0x19a4c116u; 0x1e376c08u; 0x2748774cu; 0x34b0bcb5u; 0x391c0cb3u; 0x4ed8aa4au; 0x5b9cca4fu; 0x682e6ff3u
            0x748f82eeu; 0x78a5636fu; 0x84c87814u; 0x8cc70208u; 0x90befffau; 0xa4506cebu; 0xbef9a3f7u; 0xc67178f2u
        |]

    let computeSha256Bytes (message: byte[]) =
        let mutable h0 = 0x6a09e667u // FsAssay-Ignore
        let mutable h1 = 0xbb67ae85u // FsAssay-Ignore
        let mutable h2 = 0x3c6ef372u // FsAssay-Ignore
        let mutable h3 = 0xa54ff53au // FsAssay-Ignore
        let mutable h4 = 0x510e527fu // FsAssay-Ignore
        let mutable h5 = 0x9b05688cu // FsAssay-Ignore
        let mutable h6 = 0x1f83d9abu // FsAssay-Ignore
        let mutable h7 = 0x5be0cd19u // FsAssay-Ignore

        let originalLenBits = uint64 message.Length * 8UL
        
        let padBytes =
            let rem = (message.Length + 1 + 8) % 64
            let padLen = if rem = 0 then 0 else 64 - rem
            let arr = Array.zeroCreate (1 + padLen + 8)
            arr.[0] <- 0x80uy
            for i = 0 to 7 do
                arr.[arr.Length - 1 - i] <- byte ((originalLenBits >>> (i * 8)) &&& 0xFFUL)
            arr

        let totalLen = message.Length + padBytes.Length
        let blocks = totalLen / 64

        for b = 0 to blocks - 1 do
            let w = Array.zeroCreate 64
            for i = 0 to 15 do
                let offset = b * 64 + i * 4
                let getByte j = 
                    let idx = offset + j
                    if idx < message.Length then message.[idx]
                    else padBytes.[idx - message.Length]
                w.[i] <- (uint32 (getByte 0) <<< 24) |||
                         (uint32 (getByte 1) <<< 16) |||
                         (uint32 (getByte 2) <<< 8)  |||
                         (uint32 (getByte 3))

            for i = 16 to 63 do
                let s0 = (ror32 w.[i-15] 7) ^^^ (ror32 w.[i-15] 18) ^^^ (w.[i-15] >>> 3)
                let s1 = (ror32 w.[i-2] 17) ^^^ (ror32 w.[i-2] 19) ^^^ (w.[i-2] >>> 10)
                w.[i] <- w.[i-16] + s0 + w.[i-7] + s1

            let mutable a = h0 // FsAssay-Ignore
            let mutable b_ = h1 // FsAssay-Ignore
            let mutable c = h2 // FsAssay-Ignore
            let mutable d = h3 // FsAssay-Ignore
            let mutable e = h4 // FsAssay-Ignore
            let mutable f = h5 // FsAssay-Ignore
            let mutable g = h6 // FsAssay-Ignore
            let mutable h = h7 // FsAssay-Ignore

            for i = 0 to 63 do
                let S1 = (ror32 e 6) ^^^ (ror32 e 11) ^^^ (ror32 e 25)
                let ch = (e &&& f) ^^^ ((~~~e) &&& g)
                let temp1 = h + S1 + ch + K.[i] + w.[i]
                let S0 = (ror32 a 2) ^^^ (ror32 a 13) ^^^ (ror32 a 22)
                let maj = (a &&& b_) ^^^ (a &&& c) ^^^ (b_ &&& c)
                let temp2 = S0 + maj

                h <- g
                g <- f
                f <- e
                e <- d + temp1
                d <- c
                c <- b_
                b_ <- a
                a <- temp1 + temp2

            h0 <- h0 + a
            h1 <- h1 + b_
            h2 <- h2 + c
            h3 <- h3 + d
            h4 <- h4 + e
            h5 <- h5 + f
            h6 <- h6 + g
            h7 <- h7 + h

        let result = Array.zeroCreate 32
        for i = 0 to 3 do result.[3 - i] <- byte ((h0 >>> (i * 8)) &&& 0xFFu)
        for i = 0 to 3 do result.[7 - i] <- byte ((h1 >>> (i * 8)) &&& 0xFFu)
        for i = 0 to 3 do result.[11 - i] <- byte ((h2 >>> (i * 8)) &&& 0xFFu)
        for i = 0 to 3 do result.[15 - i] <- byte ((h3 >>> (i * 8)) &&& 0xFFu)
        for i = 0 to 3 do result.[19 - i] <- byte ((h4 >>> (i * 8)) &&& 0xFFu)
        for i = 0 to 3 do result.[23 - i] <- byte ((h5 >>> (i * 8)) &&& 0xFFu)
        for i = 0 to 3 do result.[27 - i] <- byte ((h6 >>> (i * 8)) &&& 0xFFu)
        for i = 0 to 3 do result.[31 - i] <- byte ((h7 >>> (i * 8)) &&& 0xFFu)
        result

    let computeSha256 (input: string) =
        let bytes = Encoding.UTF8.GetBytes(input)
        let hashBytes = computeSha256Bytes bytes
        
        let sb = Text.StringBuilder(hashBytes.Length * 2)
        for b in hashBytes do
            sb.Append(b.ToString("x2")) |> ignore
        sb.ToString()
