module Payments.WebApi.Marten

open Marten
open Marten.Events.Projections
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Payments.Decider
open Payments.Primitives
open Payments.WebApi.View
open Weasel.Core
open Giraffe

let documentStore (connectionString: string) =
    DocumentStore.For(fun opt ->
        opt.Connection(connectionString)
        opt.AutoCreateSchemaObjects <- AutoCreate.All
        opt.Projections.Add(TransactionProjection(), ProjectionLifecycle.Inline))

let startStream: StartStream =
    fun connectionString streamId events ->
        try
            let objects = events |> List.toArray
            let ds = documentStore connectionString
            use session = ds.OpenSession()
            session.Events.StartStream(streamId, objects) |> ignore
            session.SaveChanges()
            Ok 0
        with ex ->
            Error(InfrastructureError [ ex.Message ])

let appendStream: AppendStream =
    fun connectionString streamId events ->
        try
            let objects = events |> List.toArray
            let ds = documentStore connectionString
            use session = ds.OpenSession()
            session.Events.Append(streamId, objects) |> ignore
            session.SaveChanges()
            Ok 0
        with ex ->
            Error(InfrastructureError [ ex.Message ])

let fetchStream: FetchStream =
    fun connectionString streamId ->
        try
            let ds = documentStore connectionString
            use session = ds.OpenSession()
            let rawEvents = session.Events.FetchStream(streamId)
            let events = rawEvents :> seq<_> |> Seq.map (fun e -> e.Data) |> Seq.toList
            Ok events
        with ex ->
            Error(InfrastructureError [ ex.Message ])
