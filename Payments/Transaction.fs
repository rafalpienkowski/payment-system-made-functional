module Payments.Transaction

open Payments.Primitives
open System
open Result

type PendingTransaction =
    { Id: TransactionId
      StartedAt: StartDate
      Reference: ProviderReference
      LastUpdateAt: UpdateDate
      Version: int64 }

type Transaction =
    | Initial
    | Started of
        {| Id: TransactionId
           StartedAt: StartDate
           Amount: TransactionAmount
           Version: int64 |}
    | AcknowledgeByProvider of PendingTransaction
    | SucceededByProvider of PendingTransaction
    | FailedByProvider of PendingTransaction
    | Finished of
        {| Id: TransactionId
           FinishedAt: FinishDate
           Version: int64 |}

type TransactionInitialized =
    { TransactionId: Guid
      CustomerId: Guid
      Amount: decimal
      StartedAt: DateTime
      Version: int64 }

type TransactionAcknowledged =
    { TransactionId: Guid
      ProviderReference: string
      RecordedAt: DateTime
      Version: int64 }

type TransactionSucceeded =
    { TransactionId: Guid
      RecordedAt: DateTime
      Version: int64 }

type TransactionFailed =
    { TransactionId: Guid
      RecordedAt: DateTime
      Version: int64 }

type TransactionConfirmed =
    { TransactionId: Guid
      FinishedAt: DateTime
      Version: int64 }

type TransactionDeclined =
    { TransactionId: Guid
      FinishedAt: DateTime
      Version: int64 }

type TransactionEvent =
    | Initialized of TransactionInitialized
    | Acknowledged of TransactionAcknowledged
    | Succeeded of TransactionSucceeded
    | Failed of TransactionFailed
    | Confirmed of TransactionConfirmed
    | Declined of TransactionDeclined

let evolve (transaction: Transaction) (transactionEvent: TransactionEvent) : Transaction =
    match transaction, transactionEvent with
    | Initial _, Initialized event ->
        Started
            {| Id = TransactionId.from event.TransactionId |> skipError
               StartedAt = StartDate.from event.StartedAt |> skipError
               Amount = TransactionAmount.from event.Amount |> skipError
               Version = event.Version |}
    | Started state, Acknowledged event ->
        AcknowledgeByProvider
            { Id = state.Id
              StartedAt = state.StartedAt
              Reference = ProviderReference.from event.ProviderReference |> skipError
              LastUpdateAt = UpdateDate.from event.RecordedAt |> skipError
              Version = event.Version }
    | AcknowledgeByProvider state, Succeeded event ->
        SucceededByProvider
            { state with
                LastUpdateAt = UpdateDate.from event.RecordedAt |> skipError
                Version = event.Version }
    | AcknowledgeByProvider state, Failed event ->
        FailedByProvider
            { state with
                LastUpdateAt = UpdateDate.from event.RecordedAt |> skipError
                Version = event.Version }
    | SucceededByProvider state, Confirmed event ->
        Finished
            {| Id = state.Id
               FinishedAt = FinishDate.from event.FinishedAt |> skipError
               Version = event.Version |}
    | SucceededByProvider state, Declined event ->
        Finished
            {| Id = state.Id
               FinishedAt = FinishDate.from event.FinishedAt |> skipError
               Version = event.Version |}
    | _ -> transaction
