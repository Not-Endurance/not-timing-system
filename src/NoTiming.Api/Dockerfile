FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release

COPY /src /source/src
COPY /nugets /source/nugets
COPY .editorconfig /source/.editorconfig

WORKDIR /source/src/NoTiming.Api

RUN dotnet restore "NoTiming.Api.csproj"
RUN dotnet build "NoTiming.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "NoTiming.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "NoTiming.Api.dll"]
