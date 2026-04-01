FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release

COPY /src /nexus/src
COPY /nugets /nexus/nugets
COPY .editorconfig /nexus/.editorconfig

WORKDIR /nexus/src/Apps/Nexus/NTS.Nexus.Warp

RUN dotnet restore "NTS.Nexus.Warp.csproj"
RUN dotnet build "NTS.Nexus.Warp.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "NTS.Nexus.Warp.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "NTS.Nexus.Warp.dll"]
