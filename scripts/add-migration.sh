#!/usr/bin/env bash
set -euo pipefail

SERVICE="${1:?service name required: Identity, Devices, Telemetry, Control}"
NAME="${2:?migration name required}"
CONTEXT="${SERVICE}DbContext"

dotnet tool restore
dotnet tool run dotnet-ef migrations add "$NAME" \
  -p "src/Services/${SERVICE}/TlalocAi.${SERVICE}.Infrastructure/TlalocAi.${SERVICE}.Infrastructure.csproj" \
  -s "src/Services/${SERVICE}/TlalocAi.${SERVICE}.Api/TlalocAi.${SERVICE}.Api.csproj" \
  -c "$CONTEXT" \
  -o Migrations
