module Scalfold
open System
open System.IO
open Collections


[<RequireQualifiedAccess>]
type Extension =
    | Dotnet
    | Gradle
    | Npm
    | Make
    | Docker
    | Terraform

[<RequireQualifiedAccess>]
type Target =
    | Build
    | Dist
    | Publish
    | Deploy


type Project = {
    Directory: string
    Type: Extension
    Others: Extension list
}


type ExtensionConfiguration = {
    Init: bool
    Container: string option
    Defaults: Map<string, string>
    Actions: Map<Target, string list>
}




// NOTE: order is important, first found is main extension
let extMarkers = [
    Extension.Dotnet, "*.*proj"
    Extension.Gradle, "build.gradle"
    Extension.Npm, "package.json"
    Extension.Make, "Makefile"
    Extension.Docker, "Dockerfile"
    Extension.Terraform, ".terraform.lock.hcl"
]


let extConfigs =
    Map [ 
        Extension.Dotnet, { Init = true
                            Container = Some "mcr.microsoft.com/dotnet/sdk:8.0"
                            Defaults = Map [ "configuration", "$configuration" ]
                            Actions = Map [ Target.Build, [ "build" ]
                                            Target.Dist, [ "publish" ] ] }

        Extension.Gradle, { Init = true
                            Container = Some "gradle:jdk21"
                            Defaults = Map [ "configuration", "$configuration" ]
                            Actions = Map [ Target.Build, [ "build" ]] }

        Extension.Npm, { Init = false
                         Container = Some "node:20"
                         Defaults = Map.empty
                         Actions = Map [ Target.Build, [ "build" ] ] }

        Extension.Make, { Init = false
                          Container = None
                          Defaults = Map.empty
                          Actions = Map [ Target.Build, [ "build" ] ] }

        Extension.Docker, { Init = false
                            Container = None
                            Defaults = Map [ "image", "\"ghcr.io/<orgname>/\" + $terrabuild_project"
                                             "arguments", "{ configuration: $configuration }" ]
                            Actions = Map [ Target.Dist, [ "build" ]
                                            Target.Publish, [ "push" ] ] }

        Extension.Terraform, { Init = false
                               Container = Some "hashicorp/terraform:1.7"
                               Defaults = Map.empty
                               Actions = Map [ Target.Build, [ "plan" ]
                                               Target.Deploy, [ "apply" ] ] }
    ]




let rec findProjectInDir dir =
    seq {
        let getFiles m = Directory.EnumerateFiles(dir, m)
        let isProject x = x |> Seq.isEmpty |> not

        let projects =
            extMarkers
            |> List.choose (fun (ext, pattern) ->
                if getFiles pattern |> isProject then 
                    { Directory = dir
                      Type = ext
                      Others = [] } |> Some
                else
                    None)

        match projects with
        | [ project ] ->
            yield project
        | mainProject :: others ->
            yield { mainProject 
                    with Others = others |> List.map _.Type }
        | _ ->
            for dir in Directory.EnumerateDirectories(dir) do
                yield! findProjectInDir dir
    }


let toLower s = s.ToString().ToLowerInvariant()
let toExtension (pt: Extension) = $"@{pt |> toLower}"

let genWorkspace (extensions: Extension set) =
    seq {
        "target build {"
        "  depends_on [ ^build ]"
        "}"
        ""
        "target dist {"
        "  depends_on [ build ]"
        "}"
        ""
        "target publish {"
        "  depends_on [ dist ]"
        "}"
        ""
        "target deploy {"
        "  depends_on [ publish ]"
        "}"
        ""
        "environment default {"
        "  variables {"
        "    configuration: \"Debug\""
        "  }"
        "}"
        ""
        "environment release {"
        "  variables {"
        "    configuration: \"Release\""
        "  }"
        "}"

        for extension in extensions do
            let config = extConfigs |> Map.find extension
            let container = config.Container
            let variables = config.Defaults
            let declare = container <> None || variables <> Map.empty
            if declare then
                ""
                $"extension {extension |> toExtension} {{"
                match container with
                | Some container ->
                    $"  container \"{container}\""
                | _ -> ()

                if variables <> Map.empty then
                    "  defaults {"
                    for (KeyValue(key, value)) in variables do
                        $"    {key}: {value}"
                    "  }"

                "}"
    }




let genProject (project: Project) =
    let extensions = project.Type :: project.Others

    seq {

        let config = extConfigs |> Map.find project.Type

        // determine if extension must init
        let doInit = config.Init

        // generate configuration
        if doInit then
            yield "configuration {"
            yield $"    init {project.Type |> toExtension}"
            yield "}"

        // generate targets
        let allTargets =
            extensions
            |> List.collect (fun ext ->
                extConfigs[ext].Actions
                |> Seq.map (fun kvp -> kvp.Key, ext, kvp.Value) |> List.ofSeq)
            |> List.collect (fun (targetType, ext, cmds) -> cmds |> List.map (fun cmd -> targetType, (ext, cmd)))
            |> List.groupBy (fun (targetType, _) -> targetType)
            |> Map.ofList
            |> Map.map (fun _ l -> l |> List.map snd)

        for (KeyValue(targetType, cmds)) in allTargets do
            yield ""
            yield $"target {targetType |> toLower} {{"
            for (projType, cmd) in cmds do
                yield $"    {projType |> toExtension} {cmd}"
            yield "}"
    }

let scafold workspaceDir force =
    // check we won't override files first
    if force |> not then
        let workspaceExists = workspaceDir |> IO.enumerateMatchingFiles "WORKSPACE" |> Seq.tryHead
        let projectExists = workspaceDir |> IO.enumerateMatchingFiles "PROJECT" |> Seq.tryHead

        match workspaceExists, projectExists with
        | Some file, _ -> failwith $"WORKSPACE file found '{file}'"
        | _, Some file -> failwith $"PROJECT file found '{file}'"
        | _ -> ()

    let projects = findProjectInDir workspaceDir |> List.ofSeq

    projects
    |> Seq.iter (fun project -> 
        let projectFile = IO.combinePath project.Directory "PROJECT"
        let projectContent = project |> genProject
        File.WriteAllLines(projectFile, projectContent)
    )

    let mainExtensions =
        projects
        |> List.map (fun p -> p.Type)
    let otherExtensions =
        projects
        |> List.collect (fun p -> p.Others )
    let extensions = mainExtensions @ otherExtensions |> Set

    let workspaceFile = IO.combinePath workspaceDir "WORKSPACE"
    let workspaceContent = genWorkspace extensions
    File.WriteAllLines(workspaceFile, workspaceContent)
