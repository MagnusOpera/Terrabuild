FROM mcr.microsoft.com/dotnet/aspnet:7.0.4
ARG configuration=Release

WORKDIR /app
COPY bin/${configuration}/net7.0/publish /app
ENTRYPOINT ["/app/dotnet-app"]

