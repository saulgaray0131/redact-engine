export const appConfig = {
  appName: 'RedactEngine',
  authEnabled: false,
  apiBaseUrl: import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:4000',
} as const
