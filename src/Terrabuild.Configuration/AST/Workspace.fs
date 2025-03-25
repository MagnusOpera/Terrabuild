namespace AST.Workspace
open Terrabuild.Expressions
open AST.Common



[<RequireQualifiedAccess>]
type WorkspaceBlock =
    { Id: string option
      Ignores: Set<string> option }

[<RequireQualifiedAccess>]
type TargetBlock =
    { DependsOn: Set<string>
      Rebuild: Expr option }

[<RequireQualifiedAccess>]
type ConfigurationBlock =
    { Variables: Map<string, Expr> }

[<RequireQualifiedAccess>]
type WorkspaceFile =
    { Workspace: WorkspaceBlock
      Targets: Map<string, TargetBlock>
      Configurations: Map<string, ConfigurationBlock>
      Extensions: Map<string, ExtensionBlock> }
