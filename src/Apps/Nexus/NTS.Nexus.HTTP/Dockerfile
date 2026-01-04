# Base runtime image for Azure Functions
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated8.0 AS base
WORKDIR /home/site/wwwroot
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release

COPY / /src

# Remove local-source
RUN dotnet nuget remove source "local-cache" --configfile ./src/NuGet.config
# Remove analyzers dll reference
RUN rm -f ./src/Directory.Build.props

WORKDIR /src/Nexus/NTS.Nexus.HTTP

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
