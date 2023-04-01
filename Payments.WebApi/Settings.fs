module Payments.WebApi.Settings

open Giraffe
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Payments.WebApi.Handler
open Payments.WebApi.Marten
open Payments.WebApi.Provider
open Payments.WebApi.View
open System.Text.Json
open System.Text.Json.Serialization

let connectionString =
    "User ID=postgres;Password=mysecretpassword;Host=localhost;Port=5432;Database=postgres;"

let webApp =
    choose
        [ route "/ping" >=> json {| Response = "pong" |}
          route "/" >=> GET >=> readTransactions
          route "/transactions/initialize"
          >=> POST
          >=> initializeTransactionHandler startStream acknowledgeWithProvider appendStream
          route "/transactions/post"
          >=> POST
          >=> postTransactionHandler fetchStream appendStream
          route "/transactions/confirm"
          >=> POST
          >=> confirmTransactionHandler fetchStream appendStream ]

let configureApp (app: IApplicationBuilder) = app.UseGiraffe webApp

let configureServices (services: IServiceCollection) =
    services.AddGiraffe() |> ignore
    
    let jsonOptions =
        JsonFSharpOptions.Default()
            .ToJsonSerializerOptions()
    services.AddSingleton<Json.ISerializer>(SystemTextJson.Serializer(jsonOptions)) |> ignore
