module Payments.Handler

open System
open FsToolkit.ErrorHandling
open FsToolkit.ErrorHandling.Operator.Result
open Giraffe
open Microsoft.AspNetCore.Http
open Microsoft.FSharp.Control
open Microsoft.FSharp.Core
open FsToolkit.ErrorHandling.Operator.Validation
open Payments.Primitives
open Payments.Decider
open Payments.Transaction

let validateInitializeTransactionRequest (request: InitializeTransactionDto) =
    let createInitializeTransaction transactionId customerId amount startDate : TransactionCommand =
        Initialize
            { TransactionId = transactionId
              CustomerId = customerId
              Amount = amount
              StartedAt = startDate }

    createInitializeTransaction <!^> TransactionId.from request.TransactionId
    <*^> CustomerId.from request.CustomerId
    <*^> TransactionAmount.from request.Amount
    <*^> StartDate.from request.StartedAt

let mapDateTimeToResult (dateTime: DateTime) : Result<DateTime, string> = Ok dateTime

let validatePostTransactionRequest (request: PostTransactionDto) =
    let createPostTransaction transactionId response recorderAt : TransactionCommand =
        Post
            { TransactionId = transactionId
              Response = response
              Now = recorderAt }

    let mapBooleanToResponseCode (success: bool) : Result<ResponseCode, string> =
        if success then
            Ok ResponseCode.Succeeded
        else
            Ok ResponseCode.Failed

    createPostTransaction <!^> TransactionId.from request.TransactionId
    <*^> mapBooleanToResponseCode request.Succeeded
    <*^> mapDateTimeToResult request.Now

let validateConfirmTransactionRequest (request: ConfirmTransactionDto) =
    let createConfirmTransaction transactionId result recorderAt : TransactionCommand =
        Confirm
            { TransactionId = transactionId
              ConfirmationResult = result
              Now = recorderAt }

    let mapBooleanToConfirmationResult success =
        if success then
            Ok ConfirmationResult.Succeeded
        else
            Ok ConfirmationResult.Failed

    createConfirmTransaction <!^> TransactionId.from request.TransactionId
    <*^> mapBooleanToConfirmationResult request.Succeeded
    <*^> mapDateTimeToResult request.Now

let mapValidationError (v: Validation<TransactionCommand, string>) : Result<TransactionCommand, ParsingError> =
    match v with
    | Error errorValue -> Error(ParsingError errorValue)
    | Ok command -> Ok command

let createTransaction (startStream: StartStream) (transactionId: Guid) (events: TransactionEvent list) =
    let eventObjs = events |> List.map (fun e -> e :> obj)
    startStream transactionId eventObjs

let saveTransaction (appendStream: AppendStream) (transactionId: Guid) (events: TransactionEvent list) =
    let eventObjs = events |> List.map (fun e -> e :> obj)
    appendStream transactionId eventObjs

let buildTransactionFromEvents (rawEvents: obj list) : Result<Transaction, TransactionNotFoundError> =
    if rawEvents.Length = 0 then
        Error(TransactionNotFoundError [ "" ])
    else
        let initial = Initial

        let events =
            rawEvents :> seq<_> |> Seq.map (fun e -> e :?> TransactionEvent) |> Seq.toList

        let transaction = List.fold evolve initial events
        Ok(transaction)

let loadTransaction (fetchStream: FetchStream) transactionId : Result<Transaction, TransactionError> =
    result {
        let! rawEvents = fetchStream transactionId |> Result.mapError TransactionError.Infrastructure

        let! transaction =
            buildTransactionFromEvents rawEvents
            |> Result.mapError TransactionError.NotFound

        return transaction
    }

let callProvider (acknowledgeTransactionWithProvider: AcknowledgeTransactionWithProvider) (transaction: Transaction) =
    match transaction with
    | Started t -> acknowledgeTransactionWithProvider t.Id t.Amount
    | _ -> Error(ProcessingError [ "Invalid" ] |> Processing)

let produceErrorResponse (transactionError: TransactionError) (next: HttpFunc) (ctx: HttpContext) : HttpFuncResult =
    match transactionError with
    | TransactionError.Parsing parsingError -> RequestErrors.badRequest (json parsingError) next ctx
    | TransactionError.Infrastructure _ -> ServerErrors.internalError (json "Ups something went wrong") next ctx
    | TransactionError.Processing processingError -> RequestErrors.badRequest (json processingError) next ctx
    | TransactionError.NotFound _ -> RequestErrors.notFound (json "Transaction not found") next ctx

let produceResponse operationResult next ctx : HttpFuncResult =
    match operationResult with
    | Ok _ -> json {| status = "Accepted" |} next ctx
    | Error errorValue -> produceErrorResponse errorValue next ctx





let initializeTransactionHandler
    (startStream: StartStream)
    (acknowledgeTransactionWithProvider: AcknowledgeTransactionWithProvider)
    (appendStream: AppendStream)
    : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        let request =
            ctx.BindJsonAsync<InitializeTransactionDto>()
            |> Async.AwaitTask
            |> Async.RunSynchronously

        let operationResult =
            result {
                let! command =
                    request
                    |> validateInitializeTransactionRequest
                    |> mapValidationError
                    |> Result.mapError TransactionError.Parsing

                let initial = Initial
                let! initializeEvents = decide command initial |> Result.mapError TransactionError.Processing

                createTransaction startStream request.TransactionId initializeEvents
                |> Result.mapError TransactionError.Infrastructure
                |> ignore

                let transaction = List.fold evolve initial initializeEvents
                let! acknowledgeCommand = callProvider acknowledgeTransactionWithProvider transaction

                let! ackEvents =
                    decide acknowledgeCommand transaction
                    |> Result.mapError TransactionError.Processing

                return saveTransaction appendStream request.TransactionId ackEvents
            }

        produceResponse operationResult next ctx


let postTransactionHandler (fetchStream: FetchStream) (appendStream: AppendStream) : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        let request =
            ctx.BindJsonAsync<PostTransactionDto>()
            |> Async.AwaitTask
            |> Async.RunSynchronously

        let operationResult =
            result {
                let! command =
                    request
                    |> validatePostTransactionRequest
                    |> mapValidationError
                    |> Result.mapError TransactionError.Parsing

                let! transaction = loadTransaction fetchStream request.TransactionId

                let! postEvents = decide command transaction |> Result.mapError TransactionError.Processing

                return saveTransaction appendStream request.TransactionId postEvents
            }

        produceResponse operationResult next ctx

let confirmTransactionHandler (fetchStream: FetchStream) (appendStream: AppendStream) : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        let request =
            ctx.BindJsonAsync<ConfirmTransactionDto>()
            |> Async.AwaitTask
            |> Async.RunSynchronously

        let operationalResult =
            result {
                let! command =
                    request
                    |> validateConfirmTransactionRequest
                    |> mapValidationError
                    |> Result.mapError TransactionError.Parsing

                let! transaction = loadTransaction fetchStream request.TransactionId

                let! confirmEvents = decide command transaction |> Result.mapError TransactionError.Processing

                return saveTransaction appendStream request.TransactionId confirmEvents
            }

        produceResponse operationalResult next ctx
