namespace CanonFlow.Assurance.Tests

open Xunit
open CanonFlow.Assurance

module HashTests =

    [<Fact>]
    let ``Empty string hash matches expected SHA256`` () =
        let expected = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
        let actual = Hash.computeSha256 ""
        Assert.Equal(expected, actual)

    [<Fact>]
    let ``abc string hash matches expected SHA256`` () =
        let expected = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"
        let actual = Hash.computeSha256 "abc"
        Assert.Equal(expected, actual)

    [<Fact>]
    let ``Long string hash matches expected SHA256`` () =
        let input = "abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq"
        let expected = "248d6a61d20638b8e5c026930c3e6039a33ce45964ff2167f6ecedd419db06c1"
        let actual = Hash.computeSha256 input
        Assert.Equal(expected, actual)

