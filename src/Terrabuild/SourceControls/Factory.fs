module SourceControls.Factory

let create (local: bool): SourceControl =
    if local then Local()
    elif GitHub.Detect() then GitHub()
    else Local()
