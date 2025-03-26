module FrontEnd.Workspace

open FSharp.Text.Lexing
open Errors
open System.Text

let parse txt =
    let hcl = HCL.parse txt
    Transpiler.Workspace.transpile hcl.Blocks
