module ``Initialize transaction should``

open System
open System.Net
open System.Net.Http
open System.Text
open System.Text.Json
open Payments.Transaction
open Xunit
open FsUnit.Xunit
open Payments.WebApi.Tests.TestHelpers

let transactionWithNegativeAmount =
    { TransactionId = Guid.NewGuid()
      CustomerId = Guid.NewGuid()
      Amount = -1m
      StartedAt = DateTime.UtcNow }

let invalidInitializeRequests: obj[] list = [ [| transactionWithNegativeAmount |] ]

[<Theory>]
[<MemberData(nameof invalidInitializeRequests)>]
let ``reject invalid request`` request =
    let httpRequest = new HttpRequestMessage(HttpMethod.Post, "/transactions/initialize")
    let json = JsonSerializer.Serialize(request)
    let content = new StringContent(json, UnicodeEncoding.UTF8, "application/json")
    httpRequest.Content <- content
    
    let response = testRequest httpRequest
    response.StatusCode |> should equal HttpStatusCode.BadRequest
