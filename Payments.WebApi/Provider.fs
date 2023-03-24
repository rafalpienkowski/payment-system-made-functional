module Payments.WebApi.Provider

open System
open Payments.Decider
open Payments.Primitives

let acknowledgeWithProvider: AcknowledgeTransactionWithProvider =
    fun transactionId transactionAmount ->
        let amount = TransactionAmount.value transactionAmount

        if amount < 10m then
            Error(InfrastructureError [ "Something went wrong" ] |> Infrastructure)
        elif amount < 50m then
            Ok(
                { TransactionId = transactionId
                  Response = ResponseCode.Failed
                  ProviderReference = ProviderReference.newValue
                  Now = DateTime.UtcNow }
                |> Acknowledge
            )
        else
            Ok(
                { TransactionId = transactionId
                  Response = ResponseCode.Succeeded
                  ProviderReference = ProviderReference.newValue
                  Now = DateTime.UtcNow }
                |> Acknowledge
            )
