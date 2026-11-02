import { createContext, useContext } from 'react'

export const AuthContext = createContext(null)

// { user, status: 'loading' | 'ready' | 'offline', notice, login, loginWithGoogle, pendingStep,
//   verifyTwoFactor, linkGoogle, register, logout, ... } (see AuthProvider.jsx)
export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth must be used inside <AuthProvider>.')
  return context
}
