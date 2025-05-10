
workspace {
    id = "edd11090a41b0291301431d0"
}


locals {
    isProd = terrabuild.configuration == "Release"
    configuration = local.isProd ? "Release" : "Debug"
}

target build {
    depends_on = [ target.^build ]
}

target test {
    depends_on = [ target.build ]
}

target dist {
    depends_on = [ target.build ]
}

target publish {
    depends_on = [ target.dist ]
}

extension @dotnet {
    container = "mcr.microsoft.com/dotnet/sdk:9.0.203"
    defaults {
        configuration = local.configuration
    }
}
