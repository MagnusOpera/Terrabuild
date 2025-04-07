module Terrabuild.Configuration.FrontEnd.Project

let parse txt =
    let hcl = Terrabuild.HCL.FrontEnd.parse txt
    Transpiler.Project.transpile hcl.Blocks

