import { useCallback, useEffect, useMemo, useState } from 'react'
import { AuthContext } from '../../hooks/useAuth.js'
import { api, setSessionEndedHandler } from '../../services/api.js'

// Knows who is signed in. On page load it asks the API (GET /api/auth/me): the
// HttpOnly session cookie is sent automatically, so a refresh keeps you signed in.
export default function AuthProvider({ children }) {
  const [user, setUser] = useState(null)
  const [status, setStatus] = useState('loading')
  const [notice, setNotice] = useState(null)

  // No answer at all (status 0) means no connection, which is not the same as
  // being signed out: the app shows an offline screen and checks again later.
  const checkSession = useCallback(
    () =>
      api.me().then(
        (me) => {
          setUser(me)
          setStatus('ready')
        },
        (error) => {
          if (error.status === 0) return setStatus('offline')
          setUser(null) // e.g. the server is up but answered with an error
          setStatus('ready')
        },
      ),
    [],
  )

  useEffect(() => {
    checkSession()
  }, [checkSession])

  useEffect(() => {
    if (status !== 'offline') return
    window.addEventListener('online', checkSession)
    return () => window.removeEventListener('online', checkSession)
  }, [status, checkSession])

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

  // Daily, weekly or monthly allowance view, saved on the account.
  const updatePreferences = useCallback(async (prefs) => {
    const me = await api.updatePreferences(prefs)
    setUser(me)
    return me
  }, [])

  const uploadAvatar = useCallback(async (blob) => {
    const me = await api.uploadAvatar(blob)
    setUser(me)
    return me
  }, [])

  const removeAvatar = useCallback(async () => {
    const me = await api.removeAvatar()
    setUser(me)
    return me
  }, [])

  // The API issues this browser a fresh cookie and signs out every other device.
  const changePassword = useCallback((form) => api.changePassword(form), [])

  // Permanent. Also forgets what this browser remembered for the account.
  const deleteAccount = useCallback(
    async (password) => {
      await api.deleteAccount({ password })
      try {
        localStorage.removeItem(`pennywise.dismissedInsights.${user?.id}`)
      } catch {
        // storage unavailable: nothing was saved there anyway
      }
      setUser(null)
      setNotice('Your account and all of its data have been deleted.')
    },
    [user],
  )

  const logout = useCallback(async () => {
    try {
      await api.logout()
    } finally {
      setUser(null)
      setNotice('You have been logged out.')
    }
  }, [])

  const value = useMemo(
    () => ({ user, status, notice, clearNotice: () => setNotice(null), retrySession: checkSession, login, register, logout, updateProfile, updatePreferences, uploadAvatar, removeAvatar, changePassword, deleteAccount }),
    [user, status, notice, checkSession, login, register, logout, updateProfile, updatePreferences, uploadAvatar, removeAvatar, changePassword, deleteAccount],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
