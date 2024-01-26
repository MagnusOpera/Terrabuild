module Helpers
open Extensions

let buildCmdLine cmd args cache =
    { CommandLine.Command = cmd
      CommandLine.Arguments = args
      CommandLine.Cache = cache }
