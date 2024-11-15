
workspace {
    space = "magnusopera/terrabuild"
}

configuration {
    variables = {
        configuration: "Debug"
    }
}

configuration dev {
    variables = {
        configuration: "Release"
    }
}

configuration prod {
    variables = {
        configuration: "Release"
    }
}

target build {
    depends_on = [^build]
}

target test {
    depends_on = [build]
}

target dist {
    depends_on = [build]
}

target publish {
    depends_on = [dist]
}

extension @dotnet {
    container = "mcr.microsoft.com/dotnet/sdk:9.0.100"
    variables = [
        "DOTNET_SKIP_FIRST_TIME_EXPERIENCE"
        "DOTNET_NOLOGO"
        "DOTNET_CLI_TELEMETRY_OPTOUT"
        "DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK"

        "TF_BUILD"
        "GITHUB_ACTIONS"
        "APPVEYOR"
        "CI"
        "TRAVIS"
        "CIRCLECI"
        "CODEBUILD_BUILD_ID"
        "AWS_REGION"
        "BUILD_ID" "BUILD_URL"
        "PROJECT_ID"
        "TEAMCITY_VERSION"
        "JB_SPACE_API_URL"
    ]
    defaults = {
        configuration: $configuration
    }
}
