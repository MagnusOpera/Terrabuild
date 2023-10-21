module Build
open System
open System.Collections.Generic
open Collections

type BuildOptions = {
    MaxConcurrency: int
    NoCache: bool
    Retry: bool
}

[<RequireQualifiedAccess>]
type TaskBuildStatus =
    | Success of nodeId:string * target:string
    | Failure of nodeId:string * target:string
    | Unfulfilled of nodeId:string

[<RequireQualifiedAccess>]
type BuildStatus =
    | Success
    | Failure

[<RequireQualifiedAccess>]
type BuildSummary = {
    Commit: string
    BranchOrTag: string
    StartedAt: DateTime
    EndedAt: DateTime
    Duration: TimeSpan
    Status: BuildStatus
    Targets: string list
    Dependencies: Map<string, TaskBuildStatus>
}

type IBuildNotification =
    abstract WaitCompletion: unit -> unit

    abstract BuildStarted: graph:Graph.WorkspaceGraph -> unit
    abstract BuildCompleted: summary:BuildSummary -> unit

    abstract NodeScheduled: node:Graph.Node -> unit
    abstract NodeDownloading: node:Graph.Node -> unit
    abstract NodeBuilding: node:Graph.Node -> unit
    abstract NodeUploading: node:Graph.Node -> unit
    abstract NodeCompleted: node:Graph.Node -> summary:Cache.TargetSummary option -> unit

let private isTaskUnsatisfied = function
    | TaskBuildStatus.Failure (depId, _) -> Some depId
    | TaskBuildStatus.Unfulfilled depId -> Some depId
    | TaskBuildStatus.Success _ -> None




type BuildQueue(maxItems: int) =
    let completion = new System.Threading.ManualResetEvent(false)
    let queueLock = obj()
    let queue = Queue<( (unit -> unit) )>()
    let mutable inFlight = 0

    member _.Enqueue (action: unit -> unit) =
        let rec trySchedule () =
            match queue.Count, inFlight with
            | (0, 0) -> completion.Set() |> ignore
            | (n, _) when 0 < n && inFlight < maxItems ->
                inFlight <- inFlight + 1
                queue.Dequeue() |> runTask
            | _ -> ()
        and runTask action =
            async {
                action()
                lock queueLock (fun () ->
                    inFlight <- inFlight - 1
                    trySchedule()
                )
            } |> Async.Start

        lock queueLock (fun () ->
            queue.Enqueue(action)
            trySchedule()
        )

    member _.WaitCompletion() =
        completion.WaitOne() |> ignore

let run (workspaceConfig: Configuration.WorkspaceConfig) (graph: Graph.WorkspaceGraph) (cache: Cache.Cache) (notification: IBuildNotification) (options: BuildOptions) =

    // compute first incoming edges
    let reverseIncomings = graph.Nodes |> Map.map (fun _ _ -> List<string>())
    for KeyValue(nodeId, node) in graph.Nodes do
        for dependency in node.Dependencies do
            reverseIncomings[dependency].Add(nodeId)

    let allNodes =
        graph.Nodes |> Map.map (fun _ _ -> ref 0)

    let refCounts =
        reverseIncomings
        |> Seq.collect (fun kvp -> kvp.Value)
        |> Seq.countBy (id)
        |> Map
        |> Map.map (fun _ value -> ref value)

    let readyNodes =
        allNodes |> Map.replace refCounts

    let isBuildSuccess = function
        | TaskBuildStatus.Success _ -> true
        | _ -> false

    // collect dependencies status
    let getDependencyStatus depId =
        let depNode = graph.Nodes[depId]
        let depCacheEntryId = $"{depNode.ProjectId}/{depNode.Configuration.Hash}/{depNode.TargetId}"
        match cache.TryGetSummary depCacheEntryId with
        | Some summary -> 
            match summary.Status with
            | Cache.TaskStatus.Success -> TaskBuildStatus.Success (depNode.Configuration.Hash, summary.Target)
            | Cache.TaskStatus.Failure -> TaskBuildStatus.Failure (depNode.Configuration.Hash, summary.Target)
        | _ -> TaskBuildStatus.Unfulfilled depId

    let buildNode (node: Graph.Node) =
        notification.NodeDownloading node
        let isAllSatisfied =
            node.Dependencies
            |> Seq.map getDependencyStatus
            |> Seq.choose isTaskUnsatisfied
            |> Seq.isEmpty

        if isAllSatisfied then
            let projectDirectory =
                match IO.combinePath workspaceConfig.Directory node.ProjectId with
                | IO.Directory projectDirectory -> projectDirectory
                | IO.File projectFile -> IO.parentDirectory projectFile
                | _ -> failwith $"Failed to find project {node.ProjectId}"

            let steps = node.Configuration.Steps |> Map.tryFind node.TargetId
            let nodeHash = node.Configuration.Hash
            let cacheEntryId = $"{node.ProjectId}/{nodeHash}/{node.TargetId}"

            match steps with
            | None ->
                let summary = { Cache.TargetSummary.Project = node.ProjectId
                                Cache.TargetSummary.Target = node.TargetId
                                Cache.TargetSummary.Steps = List.empty
                                Cache.TargetSummary.Outputs = None
                                Cache.TargetSummary.Status = Cache.TaskStatus.Success }
                let cacheEntry = cache.CreateEntry cacheEntryId
                cacheEntry.Complete summary
                Some summary
            | Some steps ->

                // check first if it's possible to restore previously built state
                let summary =
                    if options.NoCache then None
                    else
                        // take care of retrying failed tasks
                        match cache.TryGetSummary cacheEntryId with
                        | Some summary when summary.Status = Cache.TaskStatus.Failure && options.Retry -> None
                        | Some summary -> Some summary
                        | _ -> None

                // clean outputs if leaf node (otherwise outputs are layered on top of previous ones)
                if node.IsLeaf then
                    node.Configuration.Outputs
                    |> Seq.map (IO.combinePath projectDirectory)
                    |> Seq.iter IO.deleteAny

                match summary with
                | Some summary ->
                    match summary.Outputs with
                    | Some outputs ->
                        let files = IO.enumerateFiles outputs
                        IO.copyFiles projectDirectory outputs files |> ignore
                    | _ -> ()
                    Some summary

                | _ ->
                    let cacheEntry = cache.CreateEntry cacheEntryId
                    notification.NodeBuilding node

                    let beforeFiles = FileSystem.createSnapshot projectDirectory node.Configuration.Outputs

                    let stepLogs = List<Cache.StepSummary>()
                    let mutable lastExitCode = 0
                    let mutable stepIndex = 0
                    while stepIndex < steps.Length && lastExitCode = 0 do
                        let startedAt = DateTime.UtcNow
                        let step = steps[stepIndex]
                        let logFile = cacheEntry.NextLogFile()
                        stepIndex <- stepIndex + 1

                        let exitCode = Exec.execCaptureTimestampedOutput projectDirectory step.Command step.Arguments logFile
                        let endedAt = DateTime.UtcNow
                        let duration = endedAt - startedAt
                        let stepLog = { Cache.StepSummary.Command = step.Command
                                        Cache.StepSummary.Arguments = step.Arguments
                                        Cache.StepSummary.StartedAt = startedAt
                                        Cache.StepSummary.EndedAt = endedAt
                                        Cache.StepSummary.Duration = duration
                                        Cache.StepSummary.Log = logFile
                                        Cache.StepSummary.ExitCode = exitCode }
                        stepLog |> stepLogs.Add
                        lastExitCode <- exitCode

                    notification.NodeUploading node
                    let afterFiles = FileSystem.createSnapshot projectDirectory node.Configuration.Outputs

                    // keep only new or modified files
                    let newFiles = afterFiles - beforeFiles

                    // create an archive with new files
                    let entryOutputsDir = cacheEntry.Outputs
                    let outputs = IO.copyFiles entryOutputsDir projectDirectory newFiles

                    let status =
                        if lastExitCode = 0 then Cache.TaskStatus.Success
                        else Cache.TaskStatus.Failure

                    let summary = { Cache.TargetSummary.Project = node.ProjectId
                                    Cache.TargetSummary.Target = node.TargetId
                                    Cache.TargetSummary.Steps = stepLogs |> List.ofSeq
                                    Cache.TargetSummary.Outputs = outputs
                                    Cache.TargetSummary.Status = status }
                    cacheEntry.Complete summary
                    Some summary
        else
            None

    // this is the core of the build
    // schedule first nodes with no incoming edges
    // on completion schedule released nodes
    let buildQueue = BuildQueue(options.MaxConcurrency)
    let rec scheduleNode (nodeId: string) =
        let node = graph.Nodes[nodeId]

        let buildAction () = 
            let summary = buildNode node
            notification.NodeCompleted node summary

            // schedule children nodes if ready
            let triggers = reverseIncomings[nodeId]                
            for trigger in triggers do
                let newValue = System.Threading.Interlocked.Decrement(readyNodes[trigger])
                if newValue = 0 then
                    readyNodes[trigger].Value <- -1 // mark node as scheduled
                    scheduleNode trigger

        notification.NodeScheduled node
        buildQueue.Enqueue buildAction


    notification.BuildStarted graph
    let startedAt = DateTime.UtcNow

    readyNodes
    |> Map.filter (fun _ value -> value.Value = 0)
    |> Map.iter (fun key _ -> scheduleNode key)
    buildQueue.WaitCompletion()

    let headCommit = workspaceConfig.SourceControl.HeadCommit
    let branchOrTag = workspaceConfig.SourceControl.BranchOrTag

    let dependencies =
        graph.RootNodes
        |> Seq.map (fun (KeyValue(dependency, nodeId)) ->
            dependency, getDependencyStatus nodeId)
        |> Map

    let endedAt = DateTime.UtcNow
    let duration = endedAt - startedAt

    let status =
        let isSuccess = dependencies |> Seq.forall (fun (KeyValue(_, value)) -> isBuildSuccess value)
        if isSuccess then BuildStatus.Success
        else BuildStatus.Failure

    let buildInfo = { BuildSummary.Commit = headCommit
                      BuildSummary.BranchOrTag = branchOrTag
                      BuildSummary.StartedAt = startedAt
                      BuildSummary.EndedAt = endedAt
                      BuildSummary.Duration = duration
                      BuildSummary.Status = status
                      BuildSummary.Targets = graph.Targets
                      BuildSummary.Dependencies = dependencies }
    notification.BuildCompleted buildInfo
    buildInfo
