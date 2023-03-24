module Payments.Result

type ResultBuilder() =
    member this.Return(x) = Ok x
    member this.Bind(x, f) = Result.bind f x

let skipError result =
    match result with
    | Ok r -> r
    | _ -> failwith "todo"

let result = ResultBuilder()
