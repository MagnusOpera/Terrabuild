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
    | Success of string
    | Failure of string
    | Unfulfilled of string

[<RequireQualifiedAccess>]
type BuildStatus =
    | Success
    | Failure

[<RequireQualifiedAccess>]
type BuildSummary = {
    Commit: string
    StartedAt: DateTime
    EndedAt: DateTime
    Duration: TimeSpan
    Status: BuildStatus
    Target: string
    Dependencies: Map<string, TaskBuildStatus>
}

type IBuildNotification =
    abstract WaitCompletion: unit -> unit
    abstract BuildStarted: graph:Graph.WorkspaceGraph -> unit
    abstract BuildCompleted: summary:BuildSummary -> unit
    abstract BuildNodeStarted: node:Graph.Node -> unit
    abstract BuildNodeCompleted: node:Graph.Node -> summary:Cache.TargetSummary option -> unit

let private isTaskUnsatisfied = function
    | TaskBuildStatus.Failure depId -> Some depId
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
            | (n, _) when 0 < n && n < maxItems ->
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
        |> Map.ofSeq
        |> Map.map (fun _ value -> ref value)

    let readyNodes =
        allNodes |> Map.replace refCounts

    let variables = workspaceConfig.Build.Variables

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
            | Cache.TaskStatus.Success -> TaskBuildStatus.Success depCacheEntryId
            | Cache.TaskStatus.Failure -> TaskBuildStatus.Failure depCacheEntryId
        | _ -> TaskBuildStatus.Unfulfilled depCacheEntryId

    let buildNode (node: Graph.Node) =
        notification.BuildNodeStarted node

        let projectDirectory =
            match IO.combinePath workspaceConfig.Directory node.ProjectId with
            | IO.Directory projectDirectory -> projectDirectory
            | IO.File projectFile -> IO.parentDirectory projectFile
            | _ -> failwith $"Failed to find project '{node.ProjectId}"

        let steps = node.Configuration.Steps[node.TargetId]
        let nodeHash = node.Configuration.Hash
        let cacheEntryId = $"{node.ProjectId}/{nodeHash}/{node.TargetId}"
        let nodeTargetHash = cacheEntryId |> String.sha256

        let unsatisfyingDep =
            node.Dependencies
            |> Seq.map getDependencyStatus
            |> Seq.choose isTaskUnsatisfied |> Seq.tryHead

        match unsatisfyingDep with
        | None ->
            // check first if it's possible to restore previously built state
            let summary =
                if options.NoCache then None
                else
                    // take care of retrying failed tasks
                    match cache.TryGetSummary cacheEntryId with
                    | Some summary when summary.Status = Cache.TaskStatus.Failure && options.Retry -> None
                    | Some summary -> Some summary
                    | _ -> None

            let cleanOutputs () =
                node.Configuration.Outputs
                |> Seq.map (IO.combinePath projectDirectory)
                |> Seq.iter IO.deleteAny

            match summary with
            | Some summary ->
                // cleanup before restoring outputs
                cleanOutputs()

                match summary.Outputs with
                | Some outputs ->
                    let files = IO.enumerateFiles outputs
                    IO.copyFiles projectDirectory outputs files |> ignore
                | _ -> ()

                notification.BuildNodeCompleted node (Some summary)

            | _ ->
                let variables =
                    variables
                    |> Map.replace node.Configuration.Variables
                    |> Map.add "terrabuild_node_hash" nodeHash
                    |> Map.add "terrabuild_target_hash" nodeTargetHash

                let cacheEntry = cache.CreateEntry cacheEntryId

                if node.IsLeaf then
                    cleanOutputs()

                let beforeFiles = FileSystem.createSnapshot projectDirectory node.Configuration.Ignores

                let stepLogs = List<Cache.StepSummary>()
                let mutable lastExitCode = 0
                let mutable stepIndex = 0
                let usedVariables = HashSet<string>()
                while stepIndex < steps.Length && lastExitCode = 0 do
                    let startedAt = DateTime.UtcNow
                    let step = steps[stepIndex]
                    let logFile = cacheEntry.NextLogFile()
                    stepIndex <- stepIndex + 1                        

                    let setVariables s =
                        variables
                        |> Map.fold (fun step key value ->
                            let newStep = step |> String.replace $"$({key})" value
                            if newStep <> step then usedVariables.Add(key) |> ignore
                            newStep) s

                    let step = { step with Arguments = step.Arguments |> setVariables }

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

                let afterFiles = FileSystem.createSnapshot projectDirectory node.Configuration.Ignores

                // keep only new or modified files
                let newFiles = afterFiles - beforeFiles 

                // create an archive with new files
                let entryOutputsDir = cacheEntry.Outputs
                let outputs = IO.copyFiles entryOutputsDir projectDirectory newFiles

                let touchedVariables = usedVariables |> Seq.map (fun key -> key, variables[key]) |> Map.ofSeq

                let status =
                    if lastExitCode = 0 then Cache.TaskStatus.Success
                    else Cache.TaskStatus.Failure

                let summary = { Cache.TargetSummary.Project = node.ProjectId
                                Cache.TargetSummary.Target = node.TargetId
                                Cache.TargetSummary.Files = node.Configuration.Files
                                Cache.TargetSummary.Ignores = node.Configuration.Ignores
                                Cache.TargetSummary.Variables = touchedVariables
                                Cache.TargetSummary.Steps = stepLogs |> List.ofSeq
                                Cache.TargetSummary.Outputs = outputs
                                Cache.TargetSummary.Status = status }
                cacheEntry.Complete summary
                notification.BuildNodeCompleted node (Some summary)
        | Some _ ->
            notification.BuildNodeCompleted node None

    // this is the core of the build
    // schedule first nodes with no incoming edges
    // on completion schedule released nodes
    let buildQueue = BuildQueue(options.MaxConcurrency)
    let rec scheduleNode (nodeId: string) =
        let node = graph.Nodes[nodeId]

        let buildAction () = 
            // schedule node
            buildNode node

            // schedule children nodes if ready
            let triggers = reverseIncomings[nodeId]                
            for trigger in triggers do
                let newValue = System.Threading.Interlocked.Decrement(readyNodes[trigger])
                if newValue = 0 then
                    readyNodes[trigger].Value <- -1 // mark node as scheduled
                    scheduleNode trigger

        buildQueue.Enqueue buildAction


    notification.BuildStarted graph
    let startedAt = DateTime.UtcNow

    readyNodes
    |> Map.filter (fun _ value -> value.Value = 0)
    |> Map.iter (fun key _ -> scheduleNode key)
    buildQueue.WaitCompletion()


    let headCommit = Git.getHeadCommit workspaceConfig.Directory

    let dependencies =
        graph.RootNodes
        |> Seq.map (fun (KeyValue(dependency, nodeId)) -> dependency, getDependencyStatus nodeId)
        |> Map.ofSeq

    let endedAt = DateTime.UtcNow
    let duration = endedAt - startedAt

    let status =
        let isSuccess = dependencies |> Seq.forall (fun (KeyValue(_, value)) -> isBuildSuccess value)
        if isSuccess then BuildStatus.Success
        else BuildStatus.Failure

    let buildInfo = { BuildSummary.Commit = headCommit
                      BuildSummary.StartedAt = startedAt
                      BuildSummary.EndedAt = endedAt
                      BuildSummary.Duration = duration
                      BuildSummary.Status = status
                      BuildSummary.Target = graph.Target
                      BuildSummary.Dependencies = dependencies }
    notification.BuildCompleted buildInfo
