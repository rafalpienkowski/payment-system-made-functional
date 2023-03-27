module ``Transaction handler should``

open Xunit

[<Fact>]
let ``Should reject invalid initialize transaction request`` () =
    Assert.Fail