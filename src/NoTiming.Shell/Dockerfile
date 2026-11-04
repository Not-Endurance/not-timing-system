FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release

COPY /src /notiming/src
COPY /nugets /notiming/nugets
COPY .editorconfig /notiming/.editorconfig

WORKDIR /notiming/src/NoTiming.Shell

RUN dotnet restore "NoTiming.Shell.csproj"
RUN dotnet build "NoTiming.Shell.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "NoTiming.Shell.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "NoTiming.Shell.dll"]
