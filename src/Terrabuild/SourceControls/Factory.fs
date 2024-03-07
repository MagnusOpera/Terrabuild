module SourceControls.Factory

let create(): SourceControl =
    if GitHub.Detect() then
        GitHub()
    else
        Local()
