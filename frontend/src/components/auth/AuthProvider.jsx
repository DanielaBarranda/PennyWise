import { useCallback, useEffect, useMemo, useState } from 'react'
import { AuthContext } from '../../hooks/useAuth.js'
import { api, setSessionEndedHandler } from '../../services/api.js'

// Knows who is signed in. On page load it asks the API (GET /api/auth/me): the
// HttpOnly session cookie is sent automatically, so a refresh keeps you signed in.
export default function AuthProvider({ children }) {
  const [user, setUser] = useState(null)
  const [status, setStatus] = useState('loading')
  const [notice, setNotice] = useState(null)

  useEffect(() => {
    let cancelled = false
    api
      .me()
      .then((me) => !cancelled && setUser(me))
      .catch(() => !cancelled && setUser(null)) // 401 = not signed in; network errors show the login page too
      .finally(() => !cancelled && setStatus('ready'))
    return () => {
      cancelled = true
    }
  }, [])

  useEffect(() => {
    setSessionEndedHandler(() => {
      setUser((current) => {
        if (current) setNotice('Your session has ended. Please log in again.')
        return null
      })
    })
    return () => setSessionEndedHandler(() => {})
  }, [])

  const login = useCallback(async (credentials) => {
    const me = await api.login(credentials)
    setNotice(null)
    setUser(me)
    return me
  }, [])

  const register = useCallback(async (form) => {
    const me = await api.register(form)
    setNotice(null)
    setUser(me)
    return me
  }, [])

  const updateProfile = useCallback(async (profile) => {
    const me = await api.updateProfile(profile)
    setUser(me)
    return me
  }, [])

  // The API issues this browser a fresh cookie and signs out every other device.
  const changePassword = useCallback((form) => api.changePassword(form), [])

  const logout = useCallback(async () => {
    try {
      await api.logout()
    } finally {
      setUser(null)
      setNotice('You have been logged out.')
    }
  }, [])

  const value = useMemo(
    () => ({ user, status, notice, clearNotice: () => setNotice(null), login, register, logout, updateProfile, changePassword }),
    [user, status, notice, login, register, logout, updateProfile, changePassword],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
