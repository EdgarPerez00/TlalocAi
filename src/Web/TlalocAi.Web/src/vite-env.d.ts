/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_BASE_URL?: string
  readonly VITE_IDENTITY_API_BASE_URL?: string
  readonly VITE_DEVICES_API_BASE_URL?: string
  readonly VITE_TELEMETRY_API_BASE_URL?: string
  readonly VITE_CONTROL_API_BASE_URL?: string
  readonly VITE_ANALYTICS_API_BASE_URL?: string
  readonly VITE_USE_GATEWAY?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
