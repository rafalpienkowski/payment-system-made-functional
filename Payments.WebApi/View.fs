module Payments.WebApi.View

open System
open Marten
open Marten.Events.Projections
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Payments.Transaction
open Weasel.Postgresql.Tables
open Giraffe
open Npgsql.FSharp

type TransactionProjection() =
    inherit EventProjection()

    do
        let table = Table("transactions")
        table.AddColumn<Guid>("transaction_id").AsPrimaryKey |> ignore
        table.AddColumn<Guid>("customer_id") |> ignore
        table.AddColumn<DateTime>("started_at").AllowNulls |> ignore
        table.AddColumn<string>("provider_reference").AllowNulls |> ignore
        table.AddColumn<decimal>("amount") |> ignore
        table.AddColumn<string>("status") |> ignore
        table.AddColumn<DateTime>("finished_at").AllowNulls |> ignore

        base.SchemaObjects.Add table

        let projectTransaction (event: TransactionEvent) (ops: IDocumentOperations) =
            match event with
            | TransactionEvent.Initialized e ->
                ops.QueueSqlCommand(
                    "insert into transactions (transaction_id, customer_id, amount, started_at, status) values (?, ?, ?, ?, ?)",
                    e.TransactionId,
                    e.CustomerId,
                    e.Amount,
                    e.StartedAt,
                    "Initialized"
                )
            | TransactionEvent.Acknowledged e ->
                ops.QueueSqlCommand(
                    "update transactions set provider_reference = ?, status = ? where transaction_id = ?",
                    e.ProviderReference,
                    "Acknowledged",
                    e.TransactionId
                )
            | TransactionEvent.Succeeded e ->
                ops.QueueSqlCommand(
                    "update transactions set status = ? where transaction_id = ?",
                    "Succeeded",
                    e.TransactionId
                )
            | TransactionEvent.Failed e ->
                ops.QueueSqlCommand(
                    "update transactions set status = ? where transaction_id = ?",
                    "Failed",
                    e.TransactionId
                )
            | TransactionEvent.Confirmed e ->
                ops.QueueSqlCommand(
                    "update transactions set finished_at = ?, status = ? where transaction_id = ?",
                    e.FinishedAt,
                    "Confirmed",
                    e.TransactionId
                )
            | TransactionEvent.Declined e ->
                ops.QueueSqlCommand(
                    "update transactions set finished_at = ?, status = ? where transaction_id = ?",
                    e.FinishedAt,
                    "Declined",
                    e.TransactionId
                )

        let action = Action<TransactionEvent, IDocumentOperations> projectTransaction

        base.Project<TransactionEvent>(action)

type TransactionView =
    { TransactionId: Guid
      CustomerId: Guid
      StartedAt: DateTime
      FinishedAt: DateTime option
      ProviderReference: string option
      Amount: decimal
      Status: string }

let getAllTransactions (connectionString: string) : TransactionView list =
    connectionString
    |> Sql.connect
    |> Sql.query "SELECT * FROM public.transactions"
    |> Sql.execute (fun read ->
        { TransactionId = read.uuid "transaction_id"
          CustomerId = read.uuid "customer_id"
          StartedAt = read.dateTime "started_at"
          FinishedAt = read.dateTimeOrNone "finished_at"
          ProviderReference = read.stringOrNone "provider_reference"
          Amount = read.decimal "amount"
          Status = read.string "status" })

let readTransactions: HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        let config = ctx.GetService<IConfiguration>()
        let transactions = config.GetConnectionString "Database" |> getAllTransactions
        json transactions next ctx
