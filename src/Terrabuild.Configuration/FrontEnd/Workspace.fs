module Terrabuild.Configuration.FrontEnd.Workspace

let parse txt =
    let hcl = Terrabuild.HCL.FrontEnd.parse txt
    Transpiler.Workspace.transpile hcl.Blocks
