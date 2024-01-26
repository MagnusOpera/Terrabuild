module Helpers
open Terrabuild.Extensibility

let buildCmdLine cmd args =
    { CommandLine.Command = cmd
      CommandLine.Arguments = args
      CommandLine.Cache = Cacheability.Always }
