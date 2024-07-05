module SourceControls.Factory

let create (): Contracts.ISourceControl =
    if GitHub.Detect() then GitHub()
    else Local()
