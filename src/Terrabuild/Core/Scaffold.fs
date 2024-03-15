module Scalffold
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
    | Publish
    | Deploy


type Project = {
    Directory: string
    Type: Extension
    Others: Extension list
}


type ExtensionConfiguration = {
    Container: string option
    Defaults: Map<string, string>
    Actions: Map<Target, string list>
}



let targetConfigs =
    Map [
        Target.Build, [ "^build" ]
        Target.Publish, [ "build" ]
        Target.Deploy, [ "publish" ]
    ]

let envConfigs =
    Map [
        "default", Map [ "configuration", "Debug" ]
        "release", Map [ "configuration", "Release" ]
    ]


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
        Extension.Dotnet, { Container = Some "mcr.microsoft.com/dotnet/sdk:8.0"
                            Defaults = Map [ "configuration", "$configuration" ]
                            Actions = Map [ Target.Build, [ "build"; "publish" ] ] }

        Extension.Gradle, { Container = Some "gradle:jdk21"
                            Defaults = Map [ "configuration", "$configuration" ]
                            Actions = Map [ Target.Build, [ "build" ]] }

        Extension.Npm, { Container = Some "node:20"
                         Defaults = Map.empty
                         Actions = Map [ Target.Build, [ "build" ] ] }

        Extension.Make, { Container = None
                          Defaults = Map.empty
                          Actions = Map [ Target.Build, [ "build" ] ] }

        Extension.Docker, { Container = None
                            Defaults = Map [ "image", "\"ghcr.io/example/\" + $terrabuild_project"
                                             "arguments", "{ configuration: $configuration }" ]
                            Actions = Map [ Target.Build, [ "build" ]
                                            Target.Publish, [ "push" ] ] }

        Extension.Terraform, { Container = Some "hashicorp/terraform:1.7"
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
let toExtension (pt: Extension) = pt |> toLower

let genWorkspace (extensions: Extension set) =
    seq {
        for (KeyValue(target, dependsOn)) in targetConfigs do
            ""
            $"target {target |> toLower} {{"
            let listDependsOn = String.concat " " dependsOn
            $"  depends_on [ {listDependsOn} ]"
            "}"

        for (KeyValue(env, variables)) in envConfigs do
            ""
            $"environment {env} {{"
            if variables.Count > 0 then
                "  variables {"
                for (KeyValue(name, value)) in variables do
                    $"    {name}: \"{value}\""
                "  }"
            "}"

        for extension in extensions do
            let config = extConfigs |> Map.find extension
            let container = config.Container
            let variables = config.Defaults
            let declare = container <> None || variables <> Map.empty
            if declare then
                ""
                $"extension @{extension |> toExtension} {{"
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

        match extensions with
        | _ :: [] -> ()
        | main :: others ->
            yield "# WARNING: multiple project types detected!"
            yield $"# - @{main |> toExtension} (main)"
            for other in others do
                yield $"# - @{other |> toExtension}"
            yield ""
        | _ -> failwith "Missing project types" // NOTE: this can't happen

        // generate project block with default initializer
        yield $"project @{project.Type |> toExtension} {{"
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
                yield $"    @{projType |> toExtension} {cmd}"
            yield "}"
    }

let scaffold workspaceDir force =
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
        printfn $" {Ansi.Styles.green}{Ansi.Emojis.checkmark}{Ansi.Styles.reset} PROJECT {project.Directory}"
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

    printfn $" {Ansi.Styles.green}{Ansi.Emojis.checkmark}{Ansi.Styles.reset} WORKSPACE"
    let workspaceFile = IO.combinePath workspaceDir "WORKSPACE"
    let workspaceContent = genWorkspace extensions
    File.WriteAllLines(workspaceFile, workspaceContent)
