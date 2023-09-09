module Configuration
open Legivel.Attributes
open Helpers.Collections


(*
   █████████     ███████    ██████   ██████ ██████   ██████    ███████    ██████   █████
  ███░░░░░███  ███░░░░░███ ░░██████ ██████ ░░██████ ██████   ███░░░░░███ ░░██████ ░░███
 ███     ░░░  ███     ░░███ ░███░█████░███  ░███░█████░███  ███     ░░███ ░███░███ ░███
░███         ░███      ░███ ░███░░███ ░███  ░███░░███ ░███ ░███      ░███ ░███░░███░███
░███         ░███      ░███ ░███ ░░░  ░███  ░███ ░░░  ░███ ░███      ░███ ░███ ░░██████
░░███     ███░░███     ███  ░███      ░███  ░███      ░███ ░░███     ███  ░███  ░░█████
 ░░█████████  ░░░███████░   █████     █████ █████     █████ ░░░███████░   █████  ░░█████
  ░░░░░░░░░     ░░░░░░░    ░░░░░     ░░░░░ ░░░░░     ░░░░░    ░░░░░░░    ░░░░░    ░░░░░
*)

type Dependencies = list<string>

type Outputs = list<string>


(*
 ███████████  ███████████      ███████          █████ ██████████   █████████  ███████████
░░███░░░░░███░░███░░░░░███   ███░░░░░███       ░░███ ░░███░░░░░█  ███░░░░░███░█░░░███░░░█
 ░███    ░███ ░███    ░███  ███     ░░███       ░███  ░███  █ ░  ███     ░░░ ░   ░███  ░
 ░██████████  ░██████████  ░███      ░███       ░███  ░██████   ░███             ░███
 ░███░░░░░░   ░███░░░░░███ ░███      ░███       ░███  ░███░░█   ░███             ░███
 ░███         ░███    ░███ ░░███     ███  ███   ░███  ░███ ░   █░░███     ███    ░███
 █████        █████   █████ ░░░███████░  ░░████████   ██████████ ░░█████████     █████
░░░░░        ░░░░░   ░░░░░    ░░░░░░░     ░░░░░░░░   ░░░░░░░░░░   ░░░░░░░░░     ░░░░░
*)

type ProjectTargets = map<string, list<string>>

type ProjectConfiguration = {
    [<YamlField("dependencies")>] Dependencies: Dependencies option
    [<YamlField("outputs")>] Outputs: Outputs option
    [<YamlField("targets")>] Targets: ProjectTargets option
}


(*
 ███████████  █████  █████ █████ █████       ██████████
░░███░░░░░███░░███  ░░███ ░░███ ░░███       ░░███░░░░███
 ░███    ░███ ░███   ░███  ░███  ░███        ░███   ░░███
 ░██████████  ░███   ░███  ░███  ░███        ░███    ░███
 ░███░░░░░███ ░███   ░███  ░███  ░███        ░███    ░███
 ░███    ░███ ░███   ░███  ░███  ░███      █ ░███    ███
 ███████████  ░░████████   █████ ███████████ ██████████
░░░░░░░░░░░    ░░░░░░░░   ░░░░░ ░░░░░░░░░░░ ░░░░░░░░░░
*)

type StoreType =
    | [<YamlValue("local")>] Local

type Store = {
    [<YamlField("type")>] Type: StoreType
    [<YamlField("url")>] Url: string
}

type BuildTarget = {
    [<YamlField("depends-on")>] DependsOn: list<string> option
}

type BuildTargets = map<string, BuildTarget>

type BuildVariables = map<string, string>

type BuildConfiguration = {
    [<YamlField("store")>] Store: Store option
    [<YamlField("dependencies")>] Dependencies: Dependencies
    [<YamlField("targets")>] Targets: BuildTargets option
    [<YamlField("variables")>] Variables: BuildVariables option
}
