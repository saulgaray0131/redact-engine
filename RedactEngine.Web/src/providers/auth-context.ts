import { createContext } from 'react'

export interface AuthContextValue {
  isAuthenticated: boolean
  isLoading: boolean
  user: AuthUser | null
  login: () => Promise<void>
  logout: () => Promise<void>
  getAccessToken: () => Promise<string | null>
}

export interface AuthUser {
  id: string
  email: string
  name: string
}

export const AuthContext = createContext<AuthContextValue | undefined>(undefined)
