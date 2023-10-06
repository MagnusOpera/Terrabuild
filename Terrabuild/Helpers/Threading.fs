module Threading

open System

// http://stackoverflow.com/questions/3739531/how-to-limit-the-number-of-threads-created-for-an-asynchronous-seq-map-operation
let private throttle n fs =
    let n = new Threading.Semaphore(n, n)
    let throttleTask f = 
        async {
            let! ok = Async.AwaitWaitHandle(n)
            let! result = Async.Catch f
            n.Release() |> ignore
            return match result with
                   | Choice1Of2 rslt -> rslt
                   | Choice2Of2 exn  -> raise exn 
        } 

    fs |> Seq.map throttleTask 


let ParExec fn from maxThrottle =
    let results = from |> Seq.map fn
                       |> throttle maxThrottle
                       |> Async.Parallel
                       |> Async.RunSynchronously
    results
