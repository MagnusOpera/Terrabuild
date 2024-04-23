module SourceControls.Factory

let create (): Contracts.SourceControl =
    if GitHub.Detect() then GitHub()
    else Local()
