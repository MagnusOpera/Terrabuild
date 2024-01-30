module Helpers
open Terrabuild.Extensibility

let buildCmdLine cmd args =
    { Step.Command = cmd
      Step.Arguments = args
      Step.Cache = Cacheability.Always }
