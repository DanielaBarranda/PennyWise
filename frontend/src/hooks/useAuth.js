import { createContext, useContext } from 'react'

export const AuthContext = createContext(null)

// { user, status: 'loading' | 'ready', notice, login, register, logout }
export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth must be used inside <AuthProvider>.')
  return context
}
