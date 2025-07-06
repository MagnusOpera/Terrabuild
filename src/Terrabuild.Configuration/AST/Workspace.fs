namespace Terrabuild.Configuration.AST.Workspace
open Terrabuild.Configuration.AST
open Terrabuild.Expressions



[<RequireQualifiedAccess>]
type WorkspaceBlock =
    { Id: string option
      Ignores: Set<string> option }

[<RequireQualifiedAccess>]
type TargetBlock =
    { DependsOn: Set<string> option
      Rebuild: Expr option
      Managed: Expr option
      Restore: Expr option }

[<RequireQualifiedAccess>]
type WorkspaceFile =
    { Workspace: WorkspaceBlock
      Targets: Map<string, TargetBlock>
      Variables: Map<string, Expr option>
      Locals: Map<string, Expr>
      Extensions: Map<string, ExtensionBlock> }
