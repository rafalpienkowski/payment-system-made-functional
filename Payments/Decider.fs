module Payments.Decider

open System
open Payments.Primitives
open Payments.Transaction

type ConfirmationResult =
    | Failed = 0
    | Succeeded = 1

type ResponseCode =
    | Failed = 0
    | Succeeded = 1

type InitializeTransaction =
    { TransactionId: TransactionId
      Amount: TransactionAmount
      CustomerId: CustomerId
      StartedAt: StartDate }

type AcknowledgeTransaction =
    { TransactionId: TransactionId
      Response: ResponseCode
      ProviderReference: ProviderReference
      Now: DateTime }

type PostTransaction =
    { TransactionId: TransactionId
      Response: ResponseCode
      Now: DateTime }

type ConfirmTransaction =
    { TransactionId: TransactionId
      ConfirmationResult: ConfirmationResult
      Now: DateTime }

type TransactionCommand =
    | Initialize of InitializeTransaction
    | Acknowledge of AcknowledgeTransaction
    | Post of PostTransaction
    | Confirm of ConfirmTransaction

type AcknowledgeTransactionWithProvider =
    TransactionId -> TransactionAmount -> Result<TransactionCommand, TransactionError>

type StartStream = Guid -> obj list -> Result<int, InfrastructureError>
type AppendStream = Guid -> obj list -> Result<int, InfrastructureError>
type FetchStream = Guid -> Result<obj list, InfrastructureError>

let initializeTransaction
    (command: InitializeTransaction)
    transaction
    : Result<TransactionEvent list, ProcessingError> =
    match transaction with
    | Initial _ ->
        Ok
            [ Initialized
                  { TransactionId = TransactionId.value command.TransactionId
                    StartedAt = StartDate.value command.StartedAt
                    Amount = TransactionAmount.value command.Amount
                    CustomerId = CustomerId.value command.CustomerId
                    Version = 0 } ]
    | _ -> Error(ProcessingError [ "Transaction has been already initialized" ])

let acknowledgeTransaction
    (command: AcknowledgeTransaction)
    transaction
    : Result<TransactionEvent list, ProcessingError> =
    match transaction with
    | Started state ->
        Ok
            [ Acknowledged
                  { TransactionId = TransactionId.value state.Id
                    ProviderReference = ProviderReference.value command.ProviderReference
                    RecordedAt = command.Now
                    Version = 1 } ]
    | _ -> Error(ProcessingError [ "Transaction has to be initialized" ])

let postTransaction (command: PostTransaction) transaction : Result<TransactionEvent list, ProcessingError> =
    match transaction with
    | AcknowledgeByProvider state ->
        if command.Response = ResponseCode.Failed then
            Ok
                [ Failed
                      { TransactionId = TransactionId.value state.Id
                        RecordedAt = command.Now
                        Version = 2 } ]
        else
            Ok
                [ Succeeded
                      { TransactionId = TransactionId.value state.Id
                        RecordedAt = command.Now
                        Version = 2 } ]
    | _ -> Error(ProcessingError [ "Transaction has to be acknowledged by provider" ])

let confirmTransaction (command: ConfirmTransaction) transaction : Result<TransactionEvent list, ProcessingError> =
    match transaction with
    | SucceededByProvider state ->
        if command.ConfirmationResult = ConfirmationResult.Succeeded then
            Ok
                [ Confirmed
                      { TransactionId = TransactionId.value state.Id
                        FinishedAt = command.Now
                        Version = 3 } ]
        else
            Ok
                [ Declined
                      { TransactionId = TransactionId.value state.Id
                        FinishedAt = command.Now
                        Version = 3 } ]
    | _ -> Error(ProcessingError [ "Transaction has to be succeeded by provider" ])

let decide command transaction =
    match command with
    | Initialize cmd -> initializeTransaction cmd transaction
    | Acknowledge cmd -> acknowledgeTransaction cmd transaction
    | Post cmd -> postTransaction cmd transaction
    | Confirm cmd -> confirmTransaction cmd transaction
