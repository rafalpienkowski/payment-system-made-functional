module ``Transaction endpoint should``

open System
open System.Net
open System.Net.Http
open System.Text
open System.Text.Json
open Payments.Transaction
open Xunit
open FsUnit.Xunit
open Payments.WebApi.Tests.TestHelpers

let createPostRequest (url: string) dto =
    let httpRequest = new HttpRequestMessage(HttpMethod.Post, url)

    let json = JsonSerializer.Serialize(dto)
    let content = new StringContent(json, UnicodeEncoding.UTF8, "application/json")
    httpRequest.Content <- content
    httpRequest

let transactionWithNegativeAmount =
    { TransactionId = Guid.NewGuid()
      CustomerId = Guid.NewGuid()
      Amount = -1m
      StartedAt = DateTime.UtcNow }

let transactionFromFuture =
    { TransactionId = Guid.NewGuid()
      CustomerId = Guid.NewGuid()
      Amount = 1m
      StartedAt = DateTime.UtcNow.AddDays(10) }

let transactionFromFutureWithNegativeAmount =
    { TransactionId = Guid.NewGuid()
      CustomerId = Guid.NewGuid()
      Amount = -1m
      StartedAt = DateTime.UtcNow.AddDays(10) }

let invalidInitializeRequests: obj[] list =
    [ [| transactionWithNegativeAmount |]
      [| transactionFromFuture |]
      [| transactionFromFutureWithNegativeAmount |] ]

[<Theory>]
[<MemberData(nameof invalidInitializeRequests)>]
let ``reject invalid initialize transaction request`` request =
    let httpRequest = createPostRequest "/transactions/initialize" request
    let response = testRequest httpRequest
    response.StatusCode |> should equal HttpStatusCode.BadRequest

let validTransaction =
    { TransactionId = Guid.NewGuid()
      CustomerId = Guid.NewGuid()
      Amount = 123m
      StartedAt = DateTime.UtcNow.AddMinutes(-1) }

[<Fact>]
let ``accept valid initialize transaction request and successfully acknowledge it with provider`` () =
    let httpRequest = createPostRequest "/transactions/initialize" validTransaction

    let response = testRequest httpRequest

    response.StatusCode |> should equal HttpStatusCode.OK

let validTransactionThatCausesInfrastructureError =
    { TransactionId = Guid.NewGuid()
      CustomerId = Guid.NewGuid()
      Amount = 1m
      StartedAt = DateTime.UtcNow.AddMinutes(-1) }

[<Fact>]
let ``accept valid initialize transaction request and handle infrastructure error while calling provider`` () =
    let httpRequest =
        createPostRequest "/transactions/initialize" validTransactionThatCausesInfrastructureError

    let response = testRequest httpRequest

    response.StatusCode |> should equal HttpStatusCode.InternalServerError

let validTransactionThatIsRejectedByProvider =
    { TransactionId = Guid.NewGuid()
      CustomerId = Guid.NewGuid()
      Amount = 32m
      StartedAt = DateTime.UtcNow.AddMinutes(-1) }

[<Fact>]
let ``accept valid transaction request rejected by provider`` () =
    let httpRequest =
        createPostRequest "/transactions/initialize" validTransactionThatIsRejectedByProvider

    let response = testRequest httpRequest

    response.StatusCode |> should equal HttpStatusCode.OK
