#!/bin/sh
set -eu

js_escape() {
  printf '%s' "$1" | sed 's/\\/\\\\/g; s/"/\\"/g'
}

cat > /usr/share/nginx/html/env-config.js <<EOF
window.__TLALOCAI_CONFIG__ = {
  VITE_API_BASE_URL: "$(js_escape "${VITE_API_BASE_URL:-}")",
  VITE_IDENTITY_API_BASE_URL: "$(js_escape "${VITE_IDENTITY_API_BASE_URL:-}")",
  VITE_DEVICES_API_BASE_URL: "$(js_escape "${VITE_DEVICES_API_BASE_URL:-}")",
  VITE_TELEMETRY_API_BASE_URL: "$(js_escape "${VITE_TELEMETRY_API_BASE_URL:-}")",
  VITE_CONTROL_API_BASE_URL: "$(js_escape "${VITE_CONTROL_API_BASE_URL:-}")",
  VITE_ANALYTICS_API_BASE_URL: "$(js_escape "${VITE_ANALYTICS_API_BASE_URL:-}")",
  VITE_USE_GATEWAY: "$(js_escape "${VITE_USE_GATEWAY:-true}")"
};
EOF
