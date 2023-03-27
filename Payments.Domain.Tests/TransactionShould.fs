module ``Transaction should ``

open System
open Payments.Primitives
open Xunit
open Payments.Transaction
open Payments.Decider
open Payments.Result
open FsUnit.Xunit

let transactionId = Guid.NewGuid()
let customerId = Guid.NewGuid()
let startDate = DateTime.UtcNow.AddMinutes(-5)
let finishDate = DateTime.UtcNow
let amount = 123m
let providerReference = Guid.NewGuid().ToString("N")
let recordedAt = DateTime.UtcNow.AddMinutes(-4)
let receivedFromProvider = DateTime.UtcNow.AddMinutes(-3)


let initialized =
    Initialized
        { TransactionId = transactionId
          CustomerId = customerId
          Amount = amount
          StartedAt = startDate
          Version = 0 }

let acknowledged =
    Acknowledged
        { TransactionId = transactionId
          ProviderReference = providerReference
          RecordedAt = recordedAt
          Version = 1 }

let succeeded =
    Succeeded
        { TransactionId = transactionId
          RecordedAt = receivedFromProvider
          Version = 2 }

let failed =
    Failed
        { TransactionId = transactionId
          RecordedAt = receivedFromProvider
          Version = 2 }

let confirmed =
    Confirmed
        { TransactionId = transactionId
          FinishedAt = finishDate
          Version = 3 }

let declined =
    Declined
        { TransactionId = transactionId
          FinishedAt = finishDate
          Version = 3 }

let initialize =
    { TransactionId = TransactionId.from transactionId |> skipError
      Amount = TransactionAmount.from amount |> skipError
      CustomerId = CustomerId.from customerId |> skipError
      StartedAt = StartDate.from startDate |> skipError }
    |> TransactionCommand.Initialize

[<Fact>]
let ``be initialized from init state`` () =
    decide initialize Initial
    |> should equal (Ok [ initialized ]: Result<TransactionEvent list, ProcessingError>)

let initializedTransactionEvents: obj[] list =
    [ [| [ initialized ] |]
      [| [ initialized; acknowledged ] |]
      [| [ initialized; acknowledged; succeeded ] |]
      [| [ initialized; acknowledged; failed ] |]
      [| [ initialized; acknowledged; succeeded; confirmed ] |]
      [| [ initialized; acknowledged; succeeded; declined ] |] ]

[<Theory>]
[<MemberData(nameof initializedTransactionEvents)>]
let ``block initialisation when transaction was present`` events =
    let transaction = List.fold evolve Initial events

    decide initialize transaction
    |> should
        equal
        (Error(ProcessingError [ "Transaction has been already initialized" ]): Result<TransactionEvent list, ProcessingError>)


let acknowledge =
    { TransactionId = TransactionId.from transactionId |> skipError
      Response = ResponseCode.Succeeded
      ProviderReference = ProviderReference.from providerReference |> skipError
      Now = recordedAt }
    |> TransactionCommand.Acknowledge

[<Fact>]
let ``acknowledge initialized transaction`` () =
    let transaction = List.fold evolve Initial [ initialized ]

    decide acknowledge transaction
    |> should equal (Ok [ acknowledged ]: Result<TransactionEvent list, ProcessingError>)

let alreadyAcknowledgeTransactionEvents: obj[] list =
    [ [| [ initialized; acknowledged ] |]
      [| [ initialized; acknowledged; succeeded ] |]
      [| [ initialized; acknowledged; failed ] |]
      [| [ initialized; acknowledged; succeeded; confirmed ] |]
      [| [ initialized; acknowledged; succeeded; declined ] |] ]

[<Theory>]
[<MemberData(nameof alreadyAcknowledgeTransactionEvents)>]
let ``block acknowledge when transaction isn't initialized`` events =
    let transaction = List.fold evolve Initial events

    decide acknowledge transaction
    |> should
        equal
        (Error(ProcessingError [ "Transaction has to be initialized" ]): Result<TransactionEvent list, ProcessingError>)

let postFailed =
    { TransactionId = TransactionId.from transactionId |> skipError
      Response = ResponseCode.Failed
      Now = receivedFromProvider }
    |> TransactionCommand.Post

[<Fact>]
let ``post failed transaction`` () =
    let transaction = List.fold evolve Initial [ initialized; acknowledged ]

    decide postFailed transaction
    |> should equal (Ok [ failed ]: Result<TransactionEvent list, ProcessingError>)

let postSucceeded =
    { TransactionId = TransactionId.from transactionId |> skipError
      Response = ResponseCode.Succeeded
      Now = receivedFromProvider }
    |> TransactionCommand.Post

[<Fact>]
let ``post succeeded transaction`` () =
    let transaction = List.fold evolve Initial [ initialized; acknowledged ]

    decide postSucceeded transaction
    |> should equal (Ok [ succeeded ]: Result<TransactionEvent list, ProcessingError>)

let notAcknowledgeTransactionEvents: obj[] list =
    [ [| [ initialized ] |]
      [| [ initialized; acknowledged; succeeded ] |]
      [| [ initialized; acknowledged; failed ] |]
      [| [ initialized; acknowledged; succeeded; confirmed ] |]
      [| [ initialized; acknowledged; succeeded; declined ] |] ]

[<Theory>]
[<MemberData(nameof notAcknowledgeTransactionEvents)>]
let ``block post succeeded when transaction isn't acknowledged`` events =
    let transaction = List.fold evolve Initial events

    decide postSucceeded transaction
    |> should
        equal
        (Error(ProcessingError [ "Transaction has to be acknowledged by provider" ]): Result<TransactionEvent list, ProcessingError>)

[<Theory>]
[<MemberData(nameof notAcknowledgeTransactionEvents)>]
let ``block post failed when transaction isn't acknowledged`` events =
    let transaction = List.fold evolve Initial events

    decide postFailed transaction
    |> should
        equal
        (Error(ProcessingError [ "Transaction has to be acknowledged by provider" ]): Result<TransactionEvent list, ProcessingError>)

let confirm =
    { TransactionId = TransactionId.from transactionId |> skipError
      ConfirmationResult = ConfirmationResult.Succeeded
      Now = finishDate }
    |> TransactionCommand.Confirm

[<Fact>]
let ``confirm transaction`` () =
    let transaction = List.fold evolve Initial [ initialized; acknowledged; succeeded ]

    decide confirm transaction
    |> should equal (Ok [ confirmed ]: Result<TransactionEvent list, ProcessingError>)

let decline =
    { TransactionId = TransactionId.from transactionId |> skipError
      ConfirmationResult = ConfirmationResult.Failed
      Now = finishDate }
    |> TransactionCommand.Confirm

[<Fact>]
let ``decline transaction`` () =
    let transaction = List.fold evolve Initial [ initialized; acknowledged; succeeded ]

    decide decline transaction
    |> should equal (Ok [ declined ]: Result<TransactionEvent list, ProcessingError>)

let notPostedTransactionEvents: obj[] list =
    [ [| [ initialized ] |]
      [| [ initialized; acknowledged ] |]
      [| [ initialized; acknowledged; failed ] |]
      [| [ initialized; acknowledged; succeeded; confirmed ] |]
      [| [ initialized; acknowledged; succeeded; declined ] |] ]

[<Theory>]
[<MemberData(nameof notPostedTransactionEvents)>]
let ``block confirm succeeded when transaction isn't succeeded`` events =
    let transaction = List.fold evolve Initial events

    decide confirm transaction
    |> should
        equal
        (Error(ProcessingError [ "Transaction has to be succeeded by provider" ]): Result<TransactionEvent list, ProcessingError>)

[<Theory>]
[<MemberData(nameof notPostedTransactionEvents)>]
let ``block decline succeeded when transaction isn't succeeded`` events =
    let transaction = List.fold evolve Initial events

    decide decline transaction
    |> should
        equal
        (Error(ProcessingError [ "Transaction has to be succeeded by provider" ]): Result<TransactionEvent list, ProcessingError>)
