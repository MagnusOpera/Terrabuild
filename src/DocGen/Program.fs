open System.IO
open System.Text.RegularExpressions
open FSharp.Data


type Documentation = XmlProvider<"Examples/Terrabuild.Extensions.xml">

let load (filename: string) =
    let content = File.ReadAllText(filename)
    let result = Documentation.Parse(content)
    result


type Parameter = {
    Name: string
    Required: bool
    Summary: string
    Example: string
}

type Command = {
    Name: string
    Weight: int option
    Title: string option
    Summary: string
    mutable Parameters: Parameter list
}

type Extension = {
    Name: string
    Summary: string
    mutable Commands: Command list
}


let (|Regex|_|) pattern input =
    let m = Regex.Match(input, pattern)
    if m.Success then Some(List.tail [ for g in m.Groups -> g.Value ])
    else None


let (|Extension|Command|) s =
    match s with
    // T:Terrabuild.Extensions.Docker
    | Regex "^T:Terrabuild\.Extensions\.(.+)$" [name] -> Extension (name.ToLowerInvariant())

    // M:Terrabuild.Extensions.Terraform.plan(Microsoft.FSharp.Core.FSharpOption{System.String})
    | Regex "^M:Terrabuild\.Extensions\.([^.]+)\.([^(]+)" [extension; name] -> Command (extension.ToLowerInvariant(), name.ToLowerInvariant())
    | _ -> failwith $"Unknown member kind: {s}"



let buildExtensions (members: Documentation.Member seq) =
    // first find extensions
    let extensions =
        members
        |> Seq.choose (fun m ->
            match m.Name with
            | Extension name when name <> "null" -> Some { Name = name; Summary = m.Summary.Value; Commands = List.empty }
            | _ -> None)
        |> Seq.map (fun ext -> ext.Name, ext)
        |> Map.ofSeq

    // add members
    members
    |> Seq.iter (fun m ->
        match m.Name with
        | Command (extension, name) ->
            match extensions |> Map.tryFind extension with
            | None -> if extension <> "null" then failwith $"Extension {extension} does not exist"
            | Some ext ->
                let prms =
                    m.Params
                    |> Option.ofObj
                    |> Option.defaultValue Array.empty
                    |> Seq.map (fun prm -> { Name = prm.Name
                                             Summary = prm.Value.Trim()
                                             Required = prm.Required |> Option.defaultValue false
                                             Example = prm.Example.Value })
                    |> List.ofSeq
                let cmd = { Name = name
                            Title = m.Summary.Title
                            Summary = m.Summary.Value.Trim()
                            Parameters = prms
                            Weight = m.Summary.Weight }
                ext.Commands <- ext.Commands @ [cmd]
        | _ -> ())

    extensions
    |> Map.iter (fun _ ext -> ext.Commands <- ext.Commands |> List.sortBy (fun x -> x.Weight))

    extensions



let writeCommand extensionDir (command: Command) (batchCommand: Command option) (extension: Extension) =
    match command.Name with
    | "__init__" -> ()
    | name when name.StartsWith("__") -> ()
    | _ ->
        let commandFile = Path.Combine(extensionDir, $"{command.Name}.md")
        let commandContent = [
            "---"
            match command.Name with
            | "__dispatch__" -> $"title: \"<command>\""
            | _ -> $"title: \"{command.Name}\""
            if command.Weight |> Option.isSome then $"weight: {command.Weight.Value}"
            "---"
            ""
            command.Summary

            let name =
                match command.Parameters |> List.tryFind (fun x -> x.Name = command.Name) with
                | Some nameOverride -> nameOverride.Example
                | _ -> command.Name

            match command.Name with
            | "__dispatch__" -> $"Example for command `{name}`:"
            | _ -> ()

            "```"
            match command.Parameters with
            | [] -> $"@{extension.Name} {name}"
            | prms ->
                $"@{extension.Name} {name} {{"
                for prm in prms do
                    match prm.Name with
                    | "context" -> ()
                    | _ -> $"    {prm.Name} = {prm.Example}"
                "}"
            "```"

            $"## Argument Reference"
            match command.Parameters with
            | [] -> "This command does not accept arguments."
            | prms ->
                "The following arguments are supported:"
                for prm in prms do
                    match prm.Name with
                    | "context" -> ()
                    | _ ->
                        let required = if prm.Required then "Required" else "Optional"
                        $"* `{prm.Name}` - ({required}) {prm.Summary}"

            match batchCommand with
            | Some batchCommand ->
                "## Batch Reference"
                batchCommand.Summary

                "```"
                match batchCommand.Parameters with
                | [] -> $"@{extension.Name} {name}"
                | prms ->
                    $"@{extension.Name} {name} {{"
                    for prm in prms do
                        match prm.Name with
                        | "context" -> ()
                        | _ -> $"    {prm.Name} = {prm.Example}"
                    "}"
                "```"

                ""
                "The following arguments are supported:"
                match batchCommand.Parameters with
                | [] -> "This command does not accept arguments."
                | prms ->
                    for prm in prms do
                        match prm.Name with
                        | "context" -> ()
                        | _ ->
                            let required = if prm.Required then "Required" else "Optional"
                            $"* `{prm.Name}` - ({required}) {prm.Summary}"
            | _ -> ()
        ]
        File.WriteAllLines(commandFile, commandContent)



let writeExtension extensionDir (extension: Extension) =
    // generate extension index
    let extensionFile = Path.Combine(extensionDir, "_index.md")
    let extensionContent = [
        "---"
        $"title: \"{extension.Name}\""
        "---"
        ""
        extension.Summary
        ""
        "## Available Commands"
        match extension.Commands with
        | [] -> "This extension has no commands."
        | _ ->
            "| Command | Description |"
            "|---------|-------------|"
            for cmd in extension.Commands do
                match cmd.Name with
                | "__init__" ->
                    ()
                | "__dispatch__" ->
                    $"| [&lt;command&gt;](/docs/extensions/{extension.Name}/{cmd.Name}) | {cmd.Title |> Option.defaultValue cmd.Summary} |"
                | name when name.StartsWith("__") -> ()
                | _ ->
                    $"| [{cmd.Name}](/docs/extensions/{extension.Name}/{cmd.Name}) | {cmd.Title |> Option.defaultValue cmd.Summary} |"

            match extension.Commands |> List.tryFind (fun cmd -> cmd.Name = "__init__") with
            | Some init ->
                ""
                $"## Project Initializer"
                init.Summary
                "```"
                $"configuration @{extension.Name}"
                "```"
                "Same as:"
                "```"
                $"configuration @{extension.Name} {{"
                for prm in init.Parameters do
                    $"    {prm.Name} = {prm.Example}"
                "}"
                "```"

            | _ -> ()
    ]
    File.WriteAllLines(extensionFile, extensionContent)


[<EntryPoint>]
let main args =
    if args.Length <> 2 then failwith "Usage: DocGen <xml-doc-file> <output-dir>"
    let doc = load args[0]
    let outputDir = args[1]
    if doc.Assembly.Name <> "Terrabuild.Extensions" then failwith "Expecting documentation for Terrabuild.Extensions"

    let members = doc.Members |> Option.ofObj |> Option.defaultValue Array.empty
    let extensions = buildExtensions members

    // generate files
    printfn "Generating docs"
    for (KeyValue(_, extension)) in extensions do
        let extensionDir = Path.Combine(outputDir, extension.Name)
        if Directory.Exists extensionDir |> not then Directory.CreateDirectory extensionDir |> ignore

        printfn $"  {extension.Name}"
        writeExtension extensionDir extension

        // generate extension commands
        for cmd in extension.Commands do
            let batchCmd = extension.Commands |> List.tryFind (fun x -> x.Name = $"__{cmd.Name}__")
            writeCommand extensionDir cmd batchCmd extension

    // cleanup
    printfn "Cleaning output"
    let genExtensions = extensions.Keys |> Set.ofSeq
    let folders =
        Directory.EnumerateDirectories(outputDir)
        |> Seq.map Path.GetFileName
        |> Set.ofSeq
    let removeFolders = folders - genExtensions
    for folder in removeFolders do
        let folder = Path.Combine(outputDir, folder)
        printfn $"  Removing {folder}"
        Directory.Delete(folder, true)

    for (KeyValue(_, extension)) in extensions do
        let extensionDir = Path.Combine(outputDir, extension.Name)
        let genCommands =
            extension.Commands
            |> List.map (fun cmd -> $"{cmd.Name}.md")
            |> Set.ofSeq
            |> Set.add "_index.md"
        let commands =
            Directory.EnumerateFiles(extensionDir)
            |> Seq.map Path.GetFileName
            |> Set.ofSeq
        let removeCommands = commands - genCommands
        for command in removeCommands do
            let file = Path.Combine(extensionDir, command)
            printfn $"  Removing {file}"
            File.Delete(file)

    printfn "Done"
    0
