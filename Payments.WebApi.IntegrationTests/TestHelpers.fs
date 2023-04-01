module Payments.WebApi.Tests.TestHelpers

open Microsoft.AspNetCore.TestHost
open Microsoft.AspNetCore.Hosting
open System.Net.Http

open Payments.WebApi.Settings

let getTestHost () =
    WebHostBuilder()
        .UseTestServer()
        .Configure(configureApp)
        .ConfigureServices(configureServices)

let testRequest (request: HttpRequestMessage) =
    let resp =
        task {
            use server = new TestServer(getTestHost())
            use client = server.CreateClient()
            let! response = request |> client.SendAsync
            return response
        }

    resp.Result
