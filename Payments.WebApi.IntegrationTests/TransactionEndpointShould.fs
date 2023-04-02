module ``Transaction endpoint should``

open System
open System.Net
open System.Net.Http
open System.Text
open System.Text.Json
open System.Text.Json.Serialization
open Payments.Transaction
open Payments.WebApi.View
open Xunit
open FsUnit.Xunit
open Payments.WebApi.Tests.TestHelpers

let createPostRequest (url: string) dto =
    let httpRequest = new HttpRequestMessage(HttpMethod.Post, url)

    let json = JsonSerializer.Serialize(dto)
    let content = new StringContent(json, UnicodeEncoding.UTF8, "application/json")
    httpRequest.Content <- content
    httpRequest

let options = JsonFSharpOptions.Default().ToJsonSerializerOptions()

JsonFSharpOptions.Default().AddToJsonSerializerOptions(options)

let assertTransaction (expectedTransaction: TransactionView) : unit =
    let httpRequest = new HttpRequestMessage(HttpMethod.Get, "/")
    let response = testRequest httpRequest
    let content = response.Content.ReadAsStringAsync().Result

    let transaction =
        JsonSerializer.Deserialize<TransactionView list> content
        |> List.find (fun t -> t.TransactionId = expectedTransaction.TransactionId)

    transaction.Status |> should equal expectedTransaction.Status
    transaction.Amount |> should equal expectedTransaction.Amount
    transaction.CustomerId |> should equal expectedTransaction.CustomerId
    transaction.StartedAt |> should equal expectedTransaction.StartedAt

let transactionWithNegativeAmount =
    { TransactionId = Guid.NewGuid()
      CustomerId = Guid.NewGuid()
      Amount = -1m
      StartedAt = DateTime.Now }

let transactionFromFuture =
    { TransactionId = Guid.NewGuid()
      CustomerId = Guid.NewGuid()
      Amount = 1m
      StartedAt = DateTime.Now.AddDays(10) }

let transactionFromFutureWithNegativeAmount =
    { TransactionId = Guid.NewGuid()
      CustomerId = Guid.NewGuid()
      Amount = -1m
      StartedAt = DateTime.Now.AddDays(10) }

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
      StartedAt = DateTime.Now.AddDays(-1) }

[<Fact>]
let ``accept valid initialize transaction request and successfully acknowledge it with provider`` () =
    let httpRequest = createPostRequest "/transactions/initialize" validTransaction

    let response = testRequest httpRequest

    response.StatusCode |> should equal HttpStatusCode.OK

    assertTransaction
        { TransactionId = validTransaction.TransactionId
          CustomerId = validTransaction.CustomerId
          StartedAt = validTransaction.StartedAt
          FinishedAt = None
          ProviderReference = None
          Amount = validTransaction.Amount
          Status = "Acknowledged" }

let validTransactionThatCausesInfrastructureError =
    { TransactionId = Guid.NewGuid()
      CustomerId = Guid.NewGuid()
      Amount = 1m
      StartedAt = DateTime.Now.AddDays(-1) }

[<Fact>]
let ``accept valid initialize transaction request and handle infrastructure error while calling provider`` () =
    let httpRequest =
        createPostRequest "/transactions/initialize" validTransactionThatCausesInfrastructureError

    let response = testRequest httpRequest

    response.StatusCode |> should equal HttpStatusCode.InternalServerError

    assertTransaction
        { TransactionId = validTransactionThatCausesInfrastructureError.TransactionId
          CustomerId = validTransactionThatCausesInfrastructureError.CustomerId
          StartedAt = validTransactionThatCausesInfrastructureError.StartedAt
          FinishedAt = None
          ProviderReference = None
          Amount = validTransactionThatCausesInfrastructureError.Amount
          Status = "Initialized" }

let validTransactionThatIsRejectedByProvider =
    { TransactionId = Guid.NewGuid()
      CustomerId = Guid.NewGuid()
      Amount = 32m
      StartedAt = DateTime.Now.AddDays(-1) }

[<Fact>]
let ``accept valid transaction request rejected by provider`` () =
    let httpRequest =
        createPostRequest "/transactions/initialize" validTransactionThatIsRejectedByProvider

    let response = testRequest httpRequest

    response.StatusCode |> should equal HttpStatusCode.OK

    assertTransaction
        { TransactionId = validTransactionThatIsRejectedByProvider.TransactionId
          CustomerId = validTransactionThatIsRejectedByProvider.CustomerId
          StartedAt = validTransactionThatIsRejectedByProvider.StartedAt
          FinishedAt = None
          ProviderReference = None
          Amount = validTransactionThatIsRejectedByProvider.Amount
          Status = "Acknowledged" }

let initializeAndAcknowledgeTransactionWithProvider transaction =
    let httpRequest = createPostRequest "/transactions/initialize" transaction
    let response = testRequest httpRequest
    response.StatusCode |> should equal HttpStatusCode.OK

let postSucceededTransactionWith (transactionId: Guid) =
    let httpRequest =
        createPostRequest
            "/transactions/post"
            { TransactionId = transactionId
              Succeeded = true
              Now = DateTime.Now.AddDays(-1).AddMinutes(1) }

    let response = testRequest httpRequest
    response.StatusCode |> should equal HttpStatusCode.OK

let confirmSucceededTransactionWith (transactionId: Guid) (finishedAt: DateTime)=
    let httpRequest =
        createPostRequest
            "/transactions/confirm"
            { TransactionId = transactionId
              Succeeded = true
              Now = finishedAt}

    let response = testRequest httpRequest
    response.StatusCode |> should equal HttpStatusCode.OK

[<Fact>]
let ``successfully process transaction`` () =
    initializeAndAcknowledgeTransactionWithProvider validTransaction
    postSucceededTransactionWith validTransaction.TransactionId
    let finishedAt = DateTime.Now.AddDays(-1).AddMinutes(2) 
    confirmSucceededTransactionWith validTransaction.TransactionId finishedAt

    assertTransaction
        { TransactionId = validTransaction.TransactionId
          CustomerId = validTransaction.CustomerId
          StartedAt = validTransaction.StartedAt
          FinishedAt = Some(finishedAt)
          ProviderReference = None
          Amount = validTransaction.Amount
          Status = "Confirmed" }
