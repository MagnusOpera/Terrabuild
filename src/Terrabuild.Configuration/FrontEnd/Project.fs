module FrontEnd.Project

let parse txt =
    let hcl = HCL.parse txt
    Transpiler.Project.transpile hcl.Blocks

