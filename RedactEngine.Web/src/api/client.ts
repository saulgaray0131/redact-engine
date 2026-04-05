import { appConfig } from '@/config/app-config'

class ApiClient {
  private baseUrl: string

  constructor(baseUrl: string) {
    this.baseUrl = baseUrl
  }

  private async getHeaders(): Promise<HeadersInit> {
    const headers: HeadersInit = {
      'Accept': 'application/json',
    }

    // Future: inject bearer token when auth is enabled
    // if (appConfig.authEnabled) {
    //   const token = await getAccessToken()
    //   headers['Authorization'] = `Bearer ${token}`
    // }

    return headers
  }

  async get<T>(path: string): Promise<T> {
    const headers = await this.getHeaders()
    const response = await fetch(`${this.baseUrl}${path}`, { headers })
    if (!response.ok) {
      throw new Error(`API error: ${response.status} ${response.statusText}`)
    }
    return response.json()
  }

  async post<T>(path: string, body?: unknown): Promise<T> {
    const headers = await this.getHeaders()
    const response = await fetch(`${this.baseUrl}${path}`, {
      method: 'POST',
      headers: {
        ...headers,
        'Content-Type': 'application/json',
      },
      body: body ? JSON.stringify(body) : undefined,
    })
    if (!response.ok) {
      throw new Error(`API error: ${response.status} ${response.statusText}`)
    }
    return response.json()
  }

  async postForm<T>(path: string, formData: FormData): Promise<T> {
    const headers = await this.getHeaders()
    // Don't set Content-Type for FormData — browser sets it with boundary
    const response = await fetch(`${this.baseUrl}${path}`, {
      method: 'POST',
      headers,
      body: formData,
    })
    if (!response.ok) {
      throw new Error(`API error: ${response.status} ${response.statusText}`)
    }
    return response.json()
  }

  async delete(path: string): Promise<void> {
    const headers = await this.getHeaders()
    const response = await fetch(`${this.baseUrl}${path}`, {
      method: 'DELETE',
      headers,
    })
    if (!response.ok) {
      throw new Error(`API error: ${response.status} ${response.statusText}`)
    }
  }
}

export const apiClient = new ApiClient(appConfig.apiBaseUrl)
