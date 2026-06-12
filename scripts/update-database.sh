#!/usr/bin/env bash
set -euo pipefail

dotnet tool restore
for SERVICE in Identity Devices Telemetry Control; do
  dotnet tool run dotnet-ef database update \
    -p "src/Services/${SERVICE}/TlalocAi.${SERVICE}.Infrastructure/TlalocAi.${SERVICE}.Infrastructure.csproj" \
    -s "src/Services/${SERVICE}/TlalocAi.${SERVICE}.Api/TlalocAi.${SERVICE}.Api.csproj" \
    -c "${SERVICE}DbContext"
done
