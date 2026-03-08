# Base runtime image for Azure Functions (pinned digest for reproducible local builds)
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated8.0@sha256:7b8007c5f211ece964eb602231d1aef44f676d221004a04dd989b953cc1593d1 AS base
WORKDIR /home/site/wwwroot
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0@sha256:bfb6ed602caa605141700aea7dc7d42574b74b704368e67d683c71a002123808 AS build
ARG BUILD_CONFIGURATION=Release

COPY /src /nexus/src
COPY /nugets /nexus/nugets
COPY .editorconfig /nexus/.editorconfig

WORKDIR /nexus

# Remove local-source
RUN dotnet nuget remove source "local-cache" --configfile src/NuGet.config
# Remove analyzers dll reference
RUN rm -f ./src/Directory.Build.props

WORKDIR /nexus/src/Apps/Nexus/NTS.Nexus.HTTP

# 2. Restore + Build
RUN dotnet restore "NTS.Nexus.HTTP.csproj" 
RUN dotnet build "NTS.Nexus.HTTP.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

# Publish stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "NTS.Nexus.HTTP.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final image
FROM base AS final
WORKDIR /home/site/wwwroot

COPY --from=publish /app/publish .
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true
