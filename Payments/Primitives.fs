module Payments.Primitives

open System

module ConstrainedType =
    let createGuid fieldName ctor value =
        if value = Guid.Empty then
            Error $"%s{fieldName} invalid value"
        else
            Ok(ctor value)

    let createNotEmptyString fieldName ctor value =
        if String.IsNullOrEmpty(value) then
            Error $"%s{fieldName} can not be null or empty"
        else
            Ok(ctor value)

    let createNotEmptyListOfStrings fieldName ctor (value: string list) =
        if value.Length = 0 then
            Error $"%s{fieldName} can not be empty list"
        else
            Ok(ctor value)

    let createPastDateTime fieldName ctor value =
        if value > DateTimeOffset.UtcNow then
            Error $"%s{fieldName} can not be from the future"
        else
            Ok(ctor value)

type TransactionId = private TransactionId of Guid
type CustomerId = private CustomerId of Guid
type TransactionAmount = private TransactionAmount of decimal
type ProviderReference = private ProviderReference of string
type StartDate = private StartDate of DateTimeOffset
type UpdateDate = private UpdateDate of DateTimeOffset
type FinishDate = private FinishDate of DateTimeOffset

type ParsingError = ParsingError of string list
type InfrastructureError = InfrastructureError of string list
type ProcessingError = ProcessingError of string list
type TransactionNotFoundError = TransactionNotFoundError of string list

type TransactionError =
    | Parsing of ParsingError
    | Infrastructure of InfrastructureError
    | Processing of ProcessingError
    | NotFound of TransactionNotFoundError

module TransactionId =
    let value (TransactionId transactionId) = transactionId

    let from value =
        ConstrainedType.createGuid "TransactionId" TransactionId value

module CustomerId =
    let value (CustomerId customerId) = customerId

    let from value =
        ConstrainedType.createGuid "CustomerId" CustomerId value

module TransactionAmount =
    let value (TransactionAmount amount) = amount

    let from value =
        if value <= 0m then
            Error "Transaction amount must be greater than 0"
        else
            Ok(TransactionAmount value)

module ProviderReference =
    let newValue = ProviderReference(Guid.NewGuid().ToString("N"))

    let from value =
        ConstrainedType.createNotEmptyString "ProviderReference" ProviderReference value

    let value (ProviderReference providerReference) = providerReference

module StartDate =
    let value (StartDate startDate) = startDate

    let from value =
        ConstrainedType.createPastDateTime "StartDate" StartDate value

module UpdateDate =
    let from value =
        ConstrainedType.createPastDateTime "UpdateDate" UpdateDate value

    let value (UpdateDate updateDate) = updateDate

module FinishDate =
    let from value =
        ConstrainedType.createPastDateTime "FinishDate" FinishDate value

    let value (FinishDate finishDate) = finishDate
