module Helpers
open Terrabuild.Extensibility

let buildCmdLine cmd args cache =
    { CommandLine.Command = cmd
      CommandLine.Arguments = args
      CommandLine.Cache = cache }
