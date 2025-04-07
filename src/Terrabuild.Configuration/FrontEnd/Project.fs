module Terrabuild.Configuration.FrontEnd.Project

let parse txt =
    let ast = Terrabuild.Lang.FrontEnd.parse txt
    Transpiler.Project.transpile ast.Blocks

