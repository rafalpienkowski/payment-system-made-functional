module Payments.WebApi.Marten

open Marten
open Marten.Events.Projections
open Payments.WebApi.Settings
open Payments.Handler
open Payments.Primitives
open Payments.WebApi.View
open Weasel.Core

let documentStore =
    DocumentStore.For(fun opt ->
        opt.Connection(connectionString)
        opt.AutoCreateSchemaObjects <- AutoCreate.All
        opt.Projections.Add(TransactionProjection(), ProjectionLifecycle.Inline))

let startStream: StartStream =
    fun streamId events ->
        try
            let objects = events |> List.toArray
            use session = documentStore.OpenSession()
            session.Events.StartStream(streamId, objects) |> ignore
            session.SaveChanges()
            Ok 0
        with ex -> Error(InfrastructureError [ ex.Message ])

let appendStream: AppendStream =
    fun streamId events ->
        try
            let objects = events |> List.toArray
            use session = documentStore.OpenSession()
            session.Events.Append(streamId, objects) |> ignore
            session.SaveChanges()
            Ok 0
        with ex -> Error(InfrastructureError [ ex.Message ])

let fetchStream: FetchStream =
    fun streamId ->
        try
            use session = documentStore.OpenSession()
            let rawEvents = session.Events.FetchStream(streamId)
            let events = rawEvents :> seq<_> |> Seq.map (fun e -> e.Data) |> Seq.toList
            Ok events
        with ex -> Error(InfrastructureError [ ex.Message ])