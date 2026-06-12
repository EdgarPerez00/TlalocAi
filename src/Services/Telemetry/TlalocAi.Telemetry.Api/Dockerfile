FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/Services/Telemetry/TlalocAi.Telemetry.Api/TlalocAi.Telemetry.Api.csproj -c Release -o /app/publish --disable-build-servers -p:BuildInParallel=false -p:UseSharedCompilation=false -maxcpucount:1

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TlalocAi.Telemetry.Api.dll"]
