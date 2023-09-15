module Helpers.Xml
open System.Xml.Linq

#nowarn "0077" // op_Explicit


let NsNone = XNamespace.None
let NsMsBuild = XNamespace.Get("http://schemas.microsoft.com/developer/msbuild/2003")

let inline (!>) (x : ^a) : ^b = (((^a or ^b) : (static member op_Explicit : ^a -> ^b) x))

