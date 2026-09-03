/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Base URL of the .NET backend. Unset → use the in-memory mock client. */
  readonly VITE_API_BASE?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
