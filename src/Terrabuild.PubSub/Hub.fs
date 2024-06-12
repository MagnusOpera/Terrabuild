namespace Terrabuild.PubSub
open System
open System.Collections.Generic
open System.Collections.Concurrent
open System.Threading


type private IEventQueue =
    abstract Enqueue: action:(unit -> unit) -> unit

type private EventQueue(maxConcurrency: int) as this =
    let completed = new ManualResetEvent(false)
    let queue = Queue<( (unit -> unit) )>()
    let mutable isStarted = false
    let mutable totalTasks = 0
    let mutable inFlight = 0
    let mutable lastError = null

    let rec trySchedule () =
        match queue.Count, inFlight with
        | 0, 0 -> completed.Set() |> ignore
        | n, _ when 0 < n && inFlight < maxConcurrency ->
            // feed the pipe the most we can
            let rec schedule() =
                inFlight <- inFlight + 1
                let action = queue.Dequeue()
                async {
                    let mutable error = null
                    try action()
                    with ex -> error <- ex

                    lock this (fun () ->
                        if error <> null && lastError = null then lastError <- error
                        inFlight <- inFlight - 1
                        trySchedule()
                    )
                } |> Async.Start

                if 0 < queue.Count && inFlight < maxConcurrency then
                    schedule()
            schedule()
        | _ -> ()


    interface IEventQueue with
        member _.Enqueue (action: unit -> unit) =
            lock this (fun () ->
                totalTasks <- totalTasks + 1
                queue.Enqueue(action)
                if isStarted then trySchedule()
            )

    member _.WaitCompletion() =
        let totalTasks = lock this (fun () ->
            isStarted <- true
            if totalTasks > 0 then async { trySchedule() } |> Async.Start
            totalTasks
        )
        if totalTasks > 0 then completed.WaitOne() |> ignore
        lastError |> Option.ofObj

type SignalCompleted = unit -> unit

type ISignal = interface end

type private Signal(name, eventQueue: IEventQueue) as this =
    let subscribers = Queue<SignalCompleted>()
    let mutable raised = false

    member val Name = name

    member _.IsRaised() =
        lock this (fun () -> raised)

    member _.Subscribe(onCompleted: SignalCompleted) =
        lock this (fun () -> 
            if raised then eventQueue.Enqueue(onCompleted)
            else subscribers.Enqueue(onCompleted)
        )

    member _.Raise() =
        lock this (fun () ->
            if raised then failwith "Signal is already raised"
            else
                let rec notify() =
                    match subscribers.TryDequeue() with
                    | true, subscriber ->
                        eventQueue.Enqueue(subscriber)
                        notify()
                    | _ -> ()

                raised <- true
                notify()
        )


type IComputedGetter<'T> =
    inherit ISignal
    abstract Name: string
    abstract Value: 'T with get

type IComputedSetter<'T> =
    abstract Value: 'T with set


type private Computed<'T>(name, eventQueue) =
    inherit Signal(name, eventQueue)

    let mutable value = None

    interface IComputedGetter<'T> with
        member _.Name = name

        member this.Value =
            lock this (fun () ->
                match value with
                | Some value -> value
                | _ -> failwith "Computed has no value set"            
            )

    interface IComputedSetter<'T> with
        member this.Value with set(newValue) =
            lock this (fun () ->
                match value with
                | Some _ -> failwith "Computed has already a value"
                | _ ->
                    value <- Some newValue
                    this.Raise()
            )




type private Subscription(name, eventQueue, signals: ISignal array) as this =
    inherit Signal(name, eventQueue)

    let mutable count = signals.Length

    do
        if count = 0 then base.Raise()
        else signals |> Seq.iter (fun signal -> (signal :?> Signal).Subscribe(this.Callback))

    member private _.Callback() =
        let count = lock this (fun () -> count <- count - 1; count)
        match count with
        | 0 -> base.Raise()
        | _ -> ()
 

[<RequireQualifiedAccess>]
type Status =
    | Ok
    | SubcriptionNotRaised of string
    | SubscriptionError of Exception

type IHub =
    abstract GetComputed<'T>: name:string -> IComputedGetter<'T>
    abstract CreateComputed<'T>: name:string -> IComputedSetter<'T>

    // array used because it's covariant
    abstract Subscribe: signals:ISignal array -> handler:SignalCompleted -> unit

    abstract WaitCompletion: unit -> Status


type Hub(maxConcurrency) =
    let eventQueue = EventQueue(maxConcurrency)
    let computeds = ConcurrentDictionary<string, Signal>()
    let subscriptions = ConcurrentDictionary<string, Signal>()

    interface IHub with
        member _.GetComputed<'T> name =
            let getOrAdd _ = Computed<'T>(name, eventQueue) :> Signal
            match computeds.GetOrAdd(name, getOrAdd) with
            | :? Computed<'T> as computed -> computed
            | _ -> failwith "Unexpected Signal type"

        member _.CreateComputed<'T> name =
            let getOrAdd _ = Computed<'T>(name, eventQueue) :> Signal
            match computeds.GetOrAdd(name, getOrAdd) with
            | :? Computed<'T> as computed -> 
                computed
            | _ -> failwith "Unexpected Signal type"

        member _.Subscribe signals handler =
            let name =
                match signals with
                | [| |] -> Guid.NewGuid().ToString()
                | signals ->
                    let names = signals |> Array.map (fun signal -> (signal :?> Signal).Name)
                    String.Join("/", names)
            let subscription = Subscription(name, eventQueue, signals)
            subscriptions.TryAdd(name, subscription) |> ignore
            subscription.Subscribe(handler)

        member _.WaitCompletion() =
            match eventQueue.WaitCompletion() with
            | Some exn -> Status.SubscriptionError exn
            | _ ->
                match subscriptions |> Seq.tryFind (fun kvp -> kvp.Value.IsRaised() |> not) with
                | Some notRaised -> Status.SubcriptionNotRaised notRaised.Value.Name
                | _ -> Status.Ok

with
    static member Create maxConcurrency = Hub(maxConcurrency) :> IHub
