module Helpers
open Extensions

let buildCmdLine cmd args =
    { CommandLine.Command = cmd
      CommandLine.Arguments = args
      CommandLine.Cache = Cacheability.Always }
