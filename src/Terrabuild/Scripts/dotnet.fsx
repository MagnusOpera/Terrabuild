#if !TERRABUILD_SCRIPT
#r "bin/Debug/net8.0/Terrabuild.Extensibility.dll"
#endif

open Terrabuild.Extensibility


let private buildCmdLine cmd args =
    { Action.Command = cmd
      Action.Arguments = args
      Action.Cache = Cacheability.Always }

#nowarn "0077" // op_Explicit

module Xml =

    open System.Xml.Linq
    let NsNone = XNamespace.None
    let NsMsBuild = XNamespace.Get("http://schemas.microsoft.com/developer/msbuild/2003")

    let inline (!>) (x : ^a) : ^b = (((^a or ^b) : (static member op_Explicit : ^a -> ^b) x))

open System
open System.Xml.Linq
open Xml
open System.IO

