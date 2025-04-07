module Terrabuild.Configuration.FrontEnd.Workspace

let parse txt =
    let ast = Terrabuild.Lang.FrontEnd.parse txt
    Transpiler.Workspace.transpile ast.Blocks
