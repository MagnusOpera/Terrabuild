module Helpers
open Terrabuild.Extensibility

let buildCmdLine cmd args cache =
    { Step.Command = cmd
      Step.Arguments = args
      Step.Cache = cache }
