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

type ISignal =
    abstract Name: string
    abstract IsRaised: unit -> bool
    abstract Subscribe: SignalCompleted -> unit

type ISignal<'T> =
    inherit ISignal
    abstract Value: 'T with get, set

type private Signal<'T>(name, eventQueue: IEventQueue, lazyValue: (unit -> 'T) option) as this =
    let subscribers = Queue<SignalCompleted>()
    let mutable raised = None

    interface ISignal with
        member _.Name = name

        member _.IsRaised() =
            lock this (fun () -> raised.IsSome )

        member _.Subscribe(onCompleted: SignalCompleted) =
            lock this (fun () ->
                match raised with
                | Some _ -> eventQueue.Enqueue(onCompleted)
                | _ -> subscribers.Enqueue(onCompleted)
            )

    interface ISignal<'T> with
        member _.Value
            with get () = lock this (fun () -> 
                match raised with
                | Some raised -> raised
                | _ -> 
                    match lazyValue with
                    | Some lazyValue ->
                        let value = lazyValue()
                        (this :> ISignal<'T>).Value <- value
                        value
                    | _ -> failwith "Signal is not raised")

            and set value = lock this (fun () ->
                match raised with
                | Some _ -> failwith "Signal is already raised"
                | _ -> 
                    let rec notify() =
                        match subscribers.TryDequeue() with
                        | true, subscriber ->
                            eventQueue.Enqueue(subscriber)
                            notify()
                        | _ -> ()

                    raised <- Some value
                    notify())


type private Subscription(signal: ISignal<Unit>, signals: ISignal array) as this =
    let mutable count = signals.Length

    do
        if count = 0 then signal.Value <- ()
        else signals |> Seq.iter (fun signal -> signal.Subscribe(this.Callback))

    member private _.Callback() =
        let count = lock this (fun () -> count <- count - 1; count)
        match count with
        | 0 -> signal.Value <- ()
        | _ -> ()
 

[<RequireQualifiedAccess>]
type Status =
    | Ok
    | SubcriptionNotRaised of string
    | SubscriptionError of Exception

type IHub =
    abstract GetSignal<'T>: name:string -> ISignal<'T>
    abstract Subscribe: signals:ISignal array -> handler:SignalCompleted -> unit
    abstract WaitCompletion: unit -> Status


type Hub(maxConcurrency) =
    let eventQueue = EventQueue(maxConcurrency)
    let signals = ConcurrentDictionary<string, ISignal>()
    let subscriptions = ConcurrentDictionary<string, ISignal>()

    interface IHub with
        member _.GetSignal<'T> name =
            let getOrAdd _ = Signal<'T>(name, eventQueue, None) :> ISignal
            let signal = signals.GetOrAdd(name, getOrAdd)
            match signal with
            | :? Signal<'T> as signal -> signal
            | _ -> failwith "Unexpected Signal type"

        member _.Subscribe signals handler =
            let name =
                match signals with
                | [| |] -> Guid.NewGuid().ToString()
                | signals ->
                    let names = signals |> Array.map (fun signal -> signal.Name)
                    String.Join(",", names)
            let signal = Signal<Unit>(name, eventQueue, None)
            let subscription = Subscription(signal, signals)
            subscriptions.TryAdd(name, signal) |> ignore
            (signal :> ISignal).Subscribe(handler)

        member _.WaitCompletion() =
            match eventQueue.WaitCompletion() with
            | Some exn -> Status.SubscriptionError exn
            | _ ->
                match subscriptions |> Seq.tryFind (fun kvp -> kvp.Value.IsRaised() |> not) with
                | Some notRaised -> Status.SubcriptionNotRaised notRaised.Value.Name
                | _ -> Status.Ok

with
    static member Create maxConcurrency = Hub(maxConcurrency) :> IHub
