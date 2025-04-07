module Terrabuild.Configuration.FrontEnd.Workspace

let parse txt =
    let ast = Terrabuild.Lang.FrontEnd.parse txt
    Terrabuild.Configuration.Transpiler.Workspace.transpile ast.Blocks
