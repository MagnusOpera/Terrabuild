module Build
open System
open System.Collections.Generic
open Collections

[<RequireQualifiedAccess>]
type BuildOptions = {
    MaxConcurrency: int
    NoCache: bool
    Retry: bool
}

[<RequireQualifiedAccess>]
type NodeBuildStatus =
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
    Targets: Set<string>
    RootNodes: Set<NodeBuildStatus>
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

let private isNodeUnsatisfied = function
    | NodeBuildStatus.Failure (depId, _) -> Some depId
    | NodeBuildStatus.Unfulfilled depId -> Some depId
    | NodeBuildStatus.Success _ -> None




type BuildQueue(maxItems: int) =
    let completion = new System.Threading.ManualResetEvent(false)
    let queueLock = obj()
    let queue = Queue<( (unit -> unit) )>()
    let mutable totalTasks = 0
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
            totalTasks <- totalTasks + 1
            queue.Enqueue(action)
            trySchedule()
        )

    member _.WaitCompletion() =
        let enqueuedTasks = lock queueLock (fun () -> totalTasks)
        if enqueuedTasks > 0 then completion.WaitOne() |> ignore

let run (workspaceConfig: Configuration.WorkspaceConfig) (graph: Graph.WorkspaceGraph) (cache: Cache.Cache) (notification: IBuildNotification) (options: BuildOptions) =

    // compute first incoming edges
    let reverseIncomings =
        graph.Nodes
        |> Map.map (fun _ _ -> List<string>())
    for KeyValue(nodeId, node) in graph.Nodes do
        for dependency in node.Dependencies do
            reverseIncomings[dependency].Add(nodeId)

    let allNodes =
        graph.Nodes
        |> Map.map (fun _ _ -> ref 0)

    let refCounts =
        reverseIncomings
        |> Seq.collect (fun kvp -> kvp.Value)
        |> Seq.countBy (id)
        |> Map
        |> Map.map (fun _ value -> ref value)

    let readyNodes =
        allNodes |> Map.replace refCounts

    let isBuildSuccess = function
        | NodeBuildStatus.Success _ -> true
        | _ -> false

    // collect dependencies status
    let getDependencyStatus depId =
        let node = graph.Nodes[depId]
        let step = node.Configuration.Steps |> Map.tryFind node.Target
        let stepHash = step |> Option.map (fun cl -> cl.Hash) |> Option.defaultValue "dummy"
        let cacheEntryId = $"{node.Project}/{node.Configuration.Hash}/{node.Target}/{stepHash}"
        match cache.TryGetSummary cacheEntryId with
        | Some summary -> 
            match summary.Status with
            | Cache.TaskStatus.Success -> NodeBuildStatus.Success (node.Configuration.Hash, summary.Target)
            | Cache.TaskStatus.Failure -> NodeBuildStatus.Failure (node.Configuration.Hash, summary.Target)
        | _ -> NodeBuildStatus.Unfulfilled depId

    let buildNode (node: Graph.Node) =
        notification.NodeDownloading node
        let isAllSatisfied =
            node.Dependencies
            |> Seq.map getDependencyStatus
            |> Seq.choose isNodeUnsatisfied
            |> Seq.isEmpty

        if isAllSatisfied then
            let projectDirectory =
                match IO.combinePath workspaceConfig.Directory node.Project with
                | IO.Directory projectDirectory -> projectDirectory
                | IO.File projectFile -> IO.parentDirectory projectFile
                | _ -> failwith $"Failed to find project {node.Project}"

            let step = node.Configuration.Steps |> Map.tryFind node.Target
            let stepHash = step |> Option.map (fun cl -> cl.Hash) |> Option.defaultValue "dummy"
            let nodeHash = node.Configuration.Hash
            let cacheEntryId = $"{node.Project}/{nodeHash}/{node.Target}/{stepHash}"

            match step with
            | None ->
                let summary = { Cache.TargetSummary.Project = node.Project
                                Cache.TargetSummary.Target = node.Target
                                Cache.TargetSummary.Steps = List.empty
                                Cache.TargetSummary.Outputs = None
                                Cache.TargetSummary.Status = Cache.TaskStatus.Success }
                let cacheEntry = cache.CreateEntry cacheEntryId
                cacheEntry.Complete summary
                Some summary
            | Some step ->

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
                    let mutable cmdLineIndex = 0
                    while cmdLineIndex < step.CommandLines.Length && lastExitCode = 0 do
                        let startedAt = DateTime.UtcNow
                        let commandLine = step.CommandLines[cmdLineIndex]
                        let logFile = cacheEntry.NextLogFile()
                        cmdLineIndex <- cmdLineIndex + 1

                        let workDir, cmd, args =
                            match commandLine.Container with
                            | Some container ->
                                let cmd = "docker"
                                let wsDir = IO.combinePath Environment.CurrentDirectory workspaceConfig.Directory
                                let args = $"run --entrypoint {commandLine.Command} --rm -v {wsDir}:/terrabuild -w /terrabuild/{node.Project} {container} {commandLine.Arguments}"
                                workspaceConfig.Directory, cmd, args
                            | _ ->
                                projectDirectory, commandLine.Command, commandLine.Arguments    

                        let exitCode = Exec.execCaptureTimestampedOutput workDir cmd args logFile
                        let endedAt = DateTime.UtcNow
                        let duration = endedAt - startedAt
                        let stepLog = { Cache.StepSummary.CommandLine = commandLine
                                        Cache.StepSummary.Command = cmd
                                        Cache.StepSummary.Arguments = args
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

                    let summary = { Cache.TargetSummary.Project = node.Project
                                    Cache.TargetSummary.Target = node.Target
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

    // wait only if we have something to do
    buildQueue.WaitCompletion()

    let headCommit = workspaceConfig.SourceControl.HeadCommit
    let branchOrTag = workspaceConfig.SourceControl.BranchOrTag

    let dependencies =
        graph.RootNodes
        |> Seq.map getDependencyStatus
        |> Set

    let endedAt = DateTime.UtcNow
    let duration = endedAt - startedAt

    let status =
        let isSuccess = dependencies |> Seq.forall isBuildSuccess
        if isSuccess then BuildStatus.Success
        else BuildStatus.Failure

    let buildInfo = { BuildSummary.Commit = headCommit
                      BuildSummary.BranchOrTag = branchOrTag
                      BuildSummary.StartedAt = startedAt
                      BuildSummary.EndedAt = endedAt
                      BuildSummary.Duration = duration
                      BuildSummary.Status = status
                      BuildSummary.Targets = graph.Targets
                      BuildSummary.RootNodes = dependencies }
    notification.BuildCompleted buildInfo
    buildInfo
