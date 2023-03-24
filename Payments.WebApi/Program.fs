open Microsoft.AspNetCore.Builder
open Giraffe
open Payments.Handler
open Payments.WebApi.Marten
open Payments.WebApi.Provider
open Payments.WebApi.View

let webApp =
    choose
        [ route "/ping" >=> json {| Response = "pong" |}
          route "/" >=> GET >=> readTransactions
          route "/initialize"
          >=> POST
          >=> initializeTransactionHandler startStream acknowledgeWithProvider appendStream
          route "/post" >=> POST >=> postTransactionHandler fetchStream appendStream
          route "/confirm" >=> POST >=> confirmTransactionHandler fetchStream appendStream ]

let builder = WebApplication.CreateBuilder()
builder.Services.AddGiraffe() |> ignore

let app = builder.Build()

app.UseGiraffe webApp
app.Run()
