#!/usr/bin/env bash
set -euo pipefail

SERVICES=(
  tlalocai-identity-api
  tlalocai-devices-api
  tlalocai-telemetry-api
  tlalocai-control-api
  tlalocai-analytics-api
  tlalocai-gateway-api
)

for service in "${SERVICES[@]}"; do
  docker compose build "$service"
done

docker compose up -d --no-build
