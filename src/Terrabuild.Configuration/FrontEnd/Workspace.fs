module FrontEnd.Workspace

let parse txt =
    let hcl = HCL.parse txt
    Transpiler.Workspace.transpile hcl.Blocks
