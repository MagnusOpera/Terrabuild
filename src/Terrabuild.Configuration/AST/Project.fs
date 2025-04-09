namespace Terrabuild.Configuration.AST.Project
open Terrabuild.Configuration.AST
open Terrabuild.Expressions


[<RequireQualifiedAccess>]
type ProjectBlock =
    { Init: string option
      Id: string option
      DependsOn: Set<string>
      Dependencies: Expr option
      Outputs: Expr option
      Ignores: Expr option
      Includes: Expr option
      Labels: Set<string> }


type Step =
    { Extension: string
      Command: string
      Parameters: Map<string, Expr> }

[<RequireQualifiedAccess>]
type TargetBlock =
    { Rebuild: Expr option
      Outputs: Expr option
      DependsOn: Set<string> option
      Cache: Expr option
      Steps: Step list }

[<RequireQualifiedAccess>]
type ProjectFile =
    { Project: ProjectBlock
      Extensions: Map<string, ExtensionBlock>
      Targets: Map<string, TargetBlock>
      Locals: Map<string, Expr> }

