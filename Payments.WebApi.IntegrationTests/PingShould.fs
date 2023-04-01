module ``ping should``

open System.Net
open System.Net.Http
open Xunit
open FsUnit.Xunit
open Payments.WebApi.Tests.TestHelpers

[<Fact>]
let ``return pong`` () =
    let response = testRequest (new HttpRequestMessage(HttpMethod.Get, "/ping"))
    response.StatusCode |> should equal HttpStatusCode.OK
    response.Content.ReadAsStringAsync().Result |> should equal @"{""response"":""pong""}"
