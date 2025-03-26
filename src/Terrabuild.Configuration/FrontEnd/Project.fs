module FrontEnd.Project

open FSharp.Text.Lexing
open Errors
open System.Text


let parse txt =
    let hcl = HCL.parse txt
    Transpiler.Project.transpile hcl.Blocks

