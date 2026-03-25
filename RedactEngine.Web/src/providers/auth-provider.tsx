import { createContext, useContext, useCallback } from 'react'
import { appConfig } from '@/config/app-config'

interface AuthContextValue {
  isAuthenticated: boolean
  isLoading: boolean
  user: AuthUser | null
  login: () => Promise<void>
  logout: () => Promise<void>
  getAccessToken: () => Promise<string | null>
}

interface AuthUser {
  id: string
  email: string
  name: string
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined)

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const login = useCallback(async () => {
    if (!appConfig.authEnabled) {
      console.warn('Auth is disabled. Enable it in app-config.ts.')
      return
    }
    // Future: redirect to Auth0 or custom login
  }, [])

  const logout = useCallback(async () => {
    if (!appConfig.authEnabled) return
    // Future: clear session and redirect
  }, [])

  const getAccessToken = useCallback(async (): Promise<string | null> => {
    if (!appConfig.authEnabled) return null
    // Future: return JWT token
    return null
  }, [])

  const value: AuthContextValue = {
    isAuthenticated: !appConfig.authEnabled, // When auth disabled, treat as authenticated
    isLoading: false,
    user: appConfig.authEnabled ? null : { id: 'local', email: 'local@dev', name: 'Local User' },
    login,
    logout,
    getAccessToken,
  }

  return (
    <AuthContext.Provider value={value}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider')
  }
  return context
}
