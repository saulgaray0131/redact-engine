import type { CreateClientConfig } from './client/client.gen'
import { appConfig } from './config/app-config'

export const createClientConfig: CreateClientConfig = (config) => ({
  ...config,
  baseUrl: appConfig.apiBaseUrl,
})
