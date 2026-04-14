import { useCallback } from 'react'
import { appConfig } from '@/config/app-config'
import { AuthContext, type AuthContextValue } from './auth-context'

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
    isAuthenticated: !appConfig.authEnabled,
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
