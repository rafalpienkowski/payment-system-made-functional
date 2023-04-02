module Payments.WebApi.Tests.TestHelpers

open System.IO
open Microsoft.AspNetCore.TestHost
open Microsoft.AspNetCore.Hosting
open System.Net.Http
open Microsoft.Extensions.Configuration
open Payments.WebApi.Settings

let appConfig (context: WebHostBuilderContext) (conf: IConfigurationBuilder) : unit =
    let projectDir = Directory.GetCurrentDirectory();
    let configPath = Path.Combine(projectDir, "appsettings.json");
    conf.AddJsonFile(configPath) |> ignore

let getTestHost () =
    WebHostBuilder()
        .UseTestServer()
        .Configure(configureApp)
        .ConfigureServices(configureServices)
        .ConfigureAppConfiguration(appConfig)

let testRequest (request: HttpRequestMessage) =
    let resp =
        task {
            use server = new TestServer(getTestHost ())
            use client = server.CreateClient()
            let! response = request |> client.SendAsync
            return response
        }

    resp.Result
