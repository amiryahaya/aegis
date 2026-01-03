/// <reference types="vite/client" />

interface ImportMetaEnv {
  // API Configuration
  readonly VITE_API_URL: string
  readonly VITE_API_TIMEOUT: string
  readonly VITE_SIGNALR_URL: string

  // Feature Flags
  readonly VITE_ENABLE_MOCK_DATA: string
  readonly VITE_ENABLE_DEBUG: string

  // Application
  readonly VITE_APP_NAME: string
  readonly VITE_APP_VERSION: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
