import { useEffect, useState } from 'react'
import Icon from '../common/Icon.jsx'
import { useAuth } from '../../hooks/useAuth.js'
import { useToast } from '../../hooks/useToast.js'
import { api } from '../../services/api.js'

const HIDE_KEY = 'pennywise.verifyBanner.hidden'

// Asks a signed-in user to confirm their email, with a button to send the link
// again. Shown only when the server can send email. Hiding it lasts for this
// browser session only.
export default function VerifyEmailBanner() {
  const { user } = useAuth()
  const toast = useToast()
  const [enabled, setEnabled] = useState(false)
  const [hidden, setHidden] = useState(() => {
    try {
      return sessionStorage.getItem(HIDE_KEY) === '1'
    } catch {
      return false
    }
  })
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    if (user?.emailVerified) return
    let live = true
    api.authOptions().then((o) => live && setEnabled(Boolean(o.emailEnabled)), () => {})
    return () => {
      live = false
    }
  }, [user?.emailVerified])

  if (!user || user.emailVerified || !enabled || hidden) return null

  async function resend() {
    setBusy(true)
    try {
      await api.resendVerification()
      toast.success(`We sent a new confirmation link to ${user.email}.`)
    } catch (err) {
      toast.error(err.message)
    } finally {
      setBusy(false)
    }
  }

  function hide() {
    setHidden(true)
    try {
      sessionStorage.setItem(HIDE_KEY, '1')
    } catch {
      // private mode: it simply comes back next time
    }
  }

  return (
    <div className="verify-banner" role="status">
      <Icon name="info" size={18} />
      <p>
        <strong>Confirm your email.</strong> We sent a link to {user.email}. Confirming it lets you reset your password if you
        forget it.
      </p>
      <button type="button" className="button button--secondary button--small" onClick={resend} disabled={busy}>
        {busy ? 'Sending…' : 'Send link again'}
      </button>
      <button type="button" className="icon-button" onClick={hide} aria-label="Hide for now" title="Hide for now">
        <Icon name="close" />
      </button>
    </div>
  )
}
