module ``Transaction handler should``

open System
open System.IO
open System.Net
open System.Text
open System.Text.Json
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open NSubstitute
open Payments.Decider
open Payments.Transaction
open Payments.WebApi.Handler
open Payments.WebApi.Provider
open Xunit
open Giraffe
open FsUnit.Xunit

let next: HttpFunc = Some >> Task.FromResult

let getBody (ctx: HttpContext) =
    ctx.Response.Body.Position <- 0L
    use reader = new StreamReader(ctx.Response.Body, Encoding.UTF8)
    reader.ReadToEnd()

let buildMockContext () =
    let context = Substitute.For<HttpContext>()

    context
        .RequestServices
        .GetService(typeof<Json.ISerializer>)
        .Returns(NewtonsoftJson.Serializer(NewtonsoftJson.Serializer.DefaultSettings) :> Json.ISerializer)
    |> ignore

    context
        .RequestServices
        .GetService(typeof<INegotiationConfig>)
        .Returns(DefaultNegotiationConfig())
    |> ignore

    context.Request.Headers.ReturnsForAnyArgs(new HeaderDictionary()) |> ignore
    context.Response.Body <- new MemoryStream()
    context

let getBody2 (ctx: HttpContext) =
    ctx.Response.Body.Position <- 0L
    use reader = new StreamReader(ctx.Response.Body, System.Text.Encoding.UTF8)
    reader.ReadToEnd()

let fakeStartStream: StartStream = fun streamId events -> Ok 1

let fakeAppendStream: AppendStream = fun streamId objects -> Ok 1

[<Fact>]
let ``Should accept valid initialize transaction request`` () =
    let handler =
        initializeTransactionHandler fakeStartStream acknowledgeWithProvider fakeAppendStream

    let context = buildMockContext ()

    let dto: InitializeTransactionDto =
        { TransactionId = Guid.NewGuid()
          CustomerId = Guid.NewGuid()
          Amount = 123m
          StartedAt = DateTime.UtcNow }

    let data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(dto))
    context.Request.Body <- new MemoryStream(data)

    task {
        let! response = handler next context

        response.IsSome |> should equal true
        getBody context |> should equal @"{""status"":""Accepted""}"
    }

[<Fact>]
let ``Should reject invalid initialize transaction request`` () =
    let handler =
        initializeTransactionHandler fakeStartStream acknowledgeWithProvider fakeAppendStream

    let context = buildMockContext ()

    let dto: InitializeTransactionDto =
        { TransactionId = Guid.NewGuid()
          CustomerId = Guid.NewGuid()
          Amount = -123m
          StartedAt = DateTime.UtcNow }

    let data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(dto))
    context.Request.Body <- new MemoryStream(data)

    task {
        let! response = handler next context

        response.IsSome |> should equal true

        response.Value.Response.StatusCode
        |> should equal (int HttpStatusCode.BadRequest)

        getBody context
        |> should equal @"{""case"":""ParsingError"",""fields"":[[""Transaction amount must be greater than 0""]]}}"
    }
