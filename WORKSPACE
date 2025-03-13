
workspace {
    id = "c91ea014-00c7-8bd1-1c05-656a6d327ce7"
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
    container = "mcr.microsoft.com/dotnet/sdk:9.0.201"
    variables = [
        "DOTNET_SKIP_FIRST_TIME_EXPERIENCE"
        "DOTNET_NOLOGO"
        "DOTNET_CLI_TELEMETRY_OPTOUT"
        "DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK"
        "MSBUILDDISABLENODEREUSE"

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
