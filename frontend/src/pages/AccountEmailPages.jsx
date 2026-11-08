import { useEffect, useRef, useState } from 'react'
import FormField from '../components/common/FormField.jsx'
import Icon from '../components/common/Icon.jsx'
import PasswordField from '../components/auth/PasswordField.jsx'
import PasswordRules from '../components/auth/PasswordRules.jsx'
import { useAuth } from '../hooks/useAuth.js'
import { api } from '../services/api.js'
import { PASSWORD_RULES } from '../utils/authValidation.js'

const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

// The token from a link like "#/reset-password?token=…". It is read once and
// then removed from the address bar, so it doesn't stay in the browser history.
function useLinkToken() {
  const [token] = useState(() => new URLSearchParams(window.location.hash.split('?')[1] ?? '').get('token') ?? '')
  useEffect(() => {
    if (window.location.hash.includes('?')) {
      window.history.replaceState(null, '', window.location.hash.split('?')[0])
    }
  }, [])
  return token
}

function useHeading(title) {
  const ref = useRef(null)
  useEffect(() => {
    document.title = `${title} · PennyWise`
    ref.current?.focus()
  }, [title])
  return ref
}

// "Forgot password?": asks for the email and sends a reset link. The answer is
// the same whether or not the email has an account.
export function ForgotPasswordPage() {
  const headingRef = useHeading('Forgot password')
  const [email, setEmail] = useState('')
  const [error, setError] = useState(null)
  const [sentTo, setSentTo] = useState(null)
  const [busy, setBusy] = useState(false)

  async function handleSubmit(event) {
    event.preventDefault()
    const value = email.trim()
    if (!EMAIL.test(value)) return setError('Enter a valid email address.')
    setBusy(true)
    setError(null)
    try {
      await api.forgotPassword(value)
      setSentTo(value)
    } catch (err) {
      setError(err.fieldErrors?.email ?? err.message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <>
      <h1 ref={headingRef} tabIndex={-1} className="auth__title">Forgot your password?</h1>
      {sentTo ? (
        <>
          <p className="auth__notice" role="status">
            <Icon name="check" size={18} />
            If {sentTo} has a PennyWise account, we sent it a link to choose a new password. It works for 1 hour.
          </p>
          <p className="auth__subtitle">Check your spam folder if you don’t see it in a few minutes.</p>
        </>
      ) : (
        <>
          <p className="auth__subtitle">Enter the email you use for PennyWise and we’ll send you a link to choose a new password.</p>
          <form className="auth__form" onSubmit={handleSubmit} noValidate>
            <FormField label="Email" error={error}>
              {(props) => (
                <input {...props} type="email" autoComplete="email" inputMode="email" value={email}
                  onChange={(e) => { setEmail(e.target.value); setError(null) }} autoFocus />
              )}
            </FormField>
            <button type="submit" className="button button--primary button--block" disabled={busy}>
              {busy ? 'Sending…' : 'Send reset link'}
            </button>
          </form>
        </>
      )}
      <p className="auth__switch"><a href="#/login">Back to log in</a></p>
    </>
  )
}

// Opened from the reset email: choose a new password.
export function ResetPasswordPage() {
  const headingRef = useHeading('Choose a new password')
  const token = useLinkToken()
  const [form, setForm] = useState({ password: '', confirmPassword: '' })
  const [errors, setErrors] = useState({})
  const [formError, setFormError] = useState(token ? null : 'This reset link is incomplete. Ask for a new one.')
  const [done, setDone] = useState(false)
  const [busy, setBusy] = useState(false)
  const update = (field) => (e) => {
    setForm((f) => ({ ...f, [field]: e.target.value }))
    setErrors(({ [field]: _cleared, ...rest }) => rest)
  }

  async function handleSubmit(event) {
    event.preventDefault()
    const clientErrors = {}
    if (!PASSWORD_RULES.every((r) => r.test(form.password))) {
      clientErrors.password = 'Password must be at least 8 characters and include a letter and a number.'
    }
    if (form.confirmPassword !== form.password) clientErrors.confirmPassword = 'Passwords do not match.'
    if (Object.keys(clientErrors).length) return setErrors(clientErrors)
    setBusy(true)
    setFormError(null)
    try {
      await api.resetPassword({ token, ...form })
      setDone(true)
    } catch (err) {
      setFormError(err.message)
      if (err.fieldErrors) setErrors(err.fieldErrors)
      setBusy(false)
    }
  }

  if (done) {
    return (
      <>
        <h1 ref={headingRef} tabIndex={-1} className="auth__title">Password changed</h1>
        <p className="auth__notice" role="status">
          <Icon name="check" size={18} />
          Your new password is set, and you were signed out everywhere else. Log in with it now.
        </p>
        <a className="button button--primary button--block" href="#/login">Go to log in</a>
      </>
    )
  }

  return (
    <>
      <h1 ref={headingRef} tabIndex={-1} className="auth__title">Choose a new password</h1>
      <p className="auth__subtitle">Pick a password you don’t use anywhere else.</p>
      <form className="auth__form" onSubmit={handleSubmit} noValidate>
        {formError && (
          <p className="form__error" role="alert">
            <Icon name="alert" size={18} />
            {formError}
          </p>
        )}
        <PasswordField label="New password" value={form.password} onChange={update('password')} error={errors.password}
          autoComplete="new-password" autoFocus />
        <PasswordRules password={form.password} />
        <PasswordField label="Confirm new password" value={form.confirmPassword} onChange={update('confirmPassword')}
          error={errors.confirmPassword} autoComplete="new-password" />
        <button type="submit" className="button button--primary button--block" disabled={busy || !token}>
          {busy ? 'Saving…' : 'Set new password'}
        </button>
      </form>
      <p className="auth__switch"><a href="#/forgot-password">Ask for a new link</a></p>
    </>
  )
}

// Opened from the confirmation email: confirms it straight away.
export function VerifyEmailPage() {
  const headingRef = useHeading('Confirm email')
  const token = useLinkToken()
  const { user, refreshUser } = useAuth()
  const [state, setState] = useState(token ? 'working' : 'failed')
  const [message, setMessage] = useState(token ? null : 'This confirmation link is incomplete.')
  const started = useRef(false)

  useEffect(() => {
    if (!token || started.current) return
    started.current = true
    api.verifyEmail(token).then(
      async () => {
        setState('done')
        if (user) await refreshUser()
      },
      (err) => {
        setState('failed')
        setMessage(err.message)
      },
    )
  }, [token, user, refreshUser])

  return (
    <>
      <h1 ref={headingRef} tabIndex={-1} className="auth__title">
        {state === 'done' ? 'Email confirmed' : state === 'failed' ? 'Couldn’t confirm your email' : 'Confirming your email…'}
      </h1>
      {state === 'done' && (
        <p className="auth__notice" role="status">
          <Icon name="check" size={18} />
          Thanks! Your email is confirmed.
        </p>
      )}
      {state === 'failed' && (
        <p className="form__error" role="alert">
          <Icon name="alert" size={18} />
          {message} {user ? 'You can send a new link from the banner in the app.' : 'Log in to send a new link.'}
        </p>
      )}
      {state !== 'working' && (
        <a className="button button--primary button--block" href={user ? '#/dashboard' : '#/login'}>
          {user ? 'Go to dashboard' : 'Go to log in'}
        </a>
      )}
    </>
  )
}
