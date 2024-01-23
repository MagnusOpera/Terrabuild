module Threading

let inline await t = t |> Async.AwaitTask |> Async.RunSynchronously
