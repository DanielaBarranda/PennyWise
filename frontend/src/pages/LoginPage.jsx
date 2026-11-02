import { useEffect, useRef, useState } from 'react'
import FormField from '../components/common/FormField.jsx'
import Icon from '../components/common/Icon.jsx'
import GoogleButton from '../components/auth/GoogleButton.jsx'
import PasswordField from '../components/auth/PasswordField.jsx'
import { useAuth } from '../hooks/useAuth.js'
import { useToast } from '../hooks/useToast.js'
import { validateLogin } from '../utils/authValidation.js'

// Email and password, or Google. When the account needs one more step (the
// authenticator code, or the password once to connect Google), that step
// replaces the form here; Google sign-in from the sign-up page lands here too.
export default function LoginPage() {
  const { pendingStep } = useAuth()

  useEffect(() => {
    document.title = 'Log in · PennyWise'
  }, [])

  if (pendingStep?.next === 'twoFactor') return <TwoFactorStep />
  if (pendingStep?.next === 'linkGoogle') return <LinkGoogleStep email={pendingStep.email} />
  return <PasswordStep />
}

function PasswordStep() {
  const { login, loginWithGoogle, notice, clearNotice } = useAuth()
  const toast = useToast()
  const formRef = useRef(null)
  const headingRef = useRef(null)
  const [form, setForm] = useState({ email: '', password: '' })
  const [errors, setErrors] = useState({})
  const [formError, setFormError] = useState(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    headingRef.current?.focus()
  }, [])

  // Editing a field clears its error message; the next submit checks again.
  const update = (field) => (event) => {
    setForm((f) => ({ ...f, [field]: event.target.value }))
    setErrors(({ [field]: _cleared, ...rest }) => rest)
  }

  function showErrors(fieldErrors) {
    setErrors(fieldErrors)
    requestAnimationFrame(() => formRef.current?.querySelector('[aria-invalid="true"]')?.focus())
  }

  async function handleSubmit(event) {
    event.preventDefault()
    setFormError(null)
    clearNotice()
    const clientErrors = validateLogin(form)
    if (Object.keys(clientErrors).length > 0) return showErrors(clientErrors)

    setErrors({})
    setBusy(true)
    try {
      const me = await login({ email: form.email.trim(), password: form.password })
      if (me) toast.success(`Welcome back, ${me.name}.`)
    } catch (error) {
      setBusy(false)
      setFormError(error.message)
      if (Object.keys(error.fieldErrors ?? {}).length > 0) showErrors(error.fieldErrors)
    }
  }

  async function handleGoogle(credential) {
    setFormError(null)
    clearNotice()
    setBusy(true)
    try {
      const me = await loginWithGoogle(credential)
      if (me) toast.success(`Welcome, ${me.name}.`)
    } catch (error) {
      setBusy(false)
      setFormError(error.message)
    }
  }

  return (
    <>
      <h1 ref={headingRef} tabIndex={-1} className="auth__title">
        Log in
      </h1>
      <p className="auth__subtitle">Welcome back. Pick up where you left off.</p>

      {notice && (
        <p className="auth__notice" role="status">
          <Icon name="info" size={18} />
          {notice}
        </p>
      )}

      <form ref={formRef} className="auth__form" onSubmit={handleSubmit} noValidate>
        {formError && (
          <p className="form__error" role="alert">
            <Icon name="alert" size={18} />
            {formError}
          </p>
        )}

        <GoogleButton onCredential={handleGoogle} text="continue_with" divider disabled={busy} />

        <FormField label="Email" error={errors.email}>
          {(props) => (
            <input {...props} type="email" autoComplete="email" inputMode="email" value={form.email} onChange={update('email')} />
          )}
        </FormField>

        <PasswordField
          label="Password"
          value={form.password}
          onChange={update('password')}
          error={errors.password}
          autoComplete="current-password"
        />

        <button type="submit" className="button button--primary button--block" disabled={busy}>
          {busy ? 'Logging in…' : 'Log in'}
        </button>
      </form>

      <p className="auth__switch">
        New to PennyWise? <a href="#/register">Create an account</a>
      </p>
    </>
  )
}

// The 6-digit code from the authenticator app, or a recovery code.
function TwoFactorStep() {
  const { verifyTwoFactor, cancelStep } = useAuth()
  const toast = useToast()
  const headingRef = useRef(null)
  const [code, setCode] = useState('')
  const [useRecovery, setUseRecovery] = useState(false)
  const [error, setError] = useState(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    headingRef.current?.focus()
  }, [])

  async function handleSubmit(event) {
    event.preventDefault()
    const cleaned = code.replace(/[\s-]/g, '')
    if (!useRecovery && !/^\d{6}$/.test(cleaned)) return setError('Enter the 6-digit code from your authenticator app.')
    if (useRecovery && cleaned.length !== 10) return setError('Recovery codes look like abcde-23456.')
    setError(null)
    setBusy(true)
    try {
      const me = await verifyTwoFactor(code)
      if (me) toast.success(`Welcome back, ${me.name}.`)
      if (me && useRecovery) {
        toast.warning(`That recovery code is now used up. You have ${me.recoveryCodesLeft} left.`)
      }
    } catch (err) {
      setBusy(false)
      // An expired ticket means starting over; anything else is a wrong code.
      if (err.status === 401) {
        cancelStep()
        return
      }
      setError(err.fieldErrors?.code ?? err.message)
    }
  }

  return (
    <>
      <h1 ref={headingRef} tabIndex={-1} className="auth__title">
        Two-step verification
      </h1>
      <p className="auth__subtitle">
        {useRecovery
          ? 'Enter one of the recovery codes you saved when you turned on two-step verification.'
          : 'Open your authenticator app and enter the 6-digit code for PennyWise.'}
      </p>

      <form className="auth__form" onSubmit={handleSubmit} noValidate>
        <FormField label={useRecovery ? 'Recovery code' : 'Code'} error={error}>
          {(props) =>
            useRecovery ? (
              <input {...props} key="recovery" type="text" autoComplete="off" autoCapitalize="none" spellCheck={false}
                maxLength={20} placeholder="abcde-23456" value={code} onChange={(e) => { setCode(e.target.value); setError(null) }} autoFocus />
            ) : (
              <input {...props} key="totp" className="code-input" type="text" inputMode="numeric" autoComplete="one-time-code"
                maxLength={7} placeholder="123456" value={code} onChange={(e) => { setCode(e.target.value); setError(null) }} autoFocus />
            )
          }
        </FormField>

        <button type="submit" className="button button--primary button--block" disabled={busy}>
          {busy ? 'Checking…' : 'Verify and log in'}
        </button>
      </form>

      <p className="auth__switch">
        <button type="button" className="button button--link" onClick={() => { setUseRecovery((r) => !r); setCode(''); setError(null) }}>
          {useRecovery ? 'Use the authenticator app instead' : 'Lost your phone? Use a recovery code'}
        </button>
      </p>
      <p className="auth__switch auth__switch--tight">
        <button type="button" className="button button--link" onClick={cancelStep}>
          Back to log in
        </button>
      </p>
    </>
  )
}

// First Google sign-in for an email that already has a PennyWise password:
// the password is asked for once, then Google signs straight in from now on.
function LinkGoogleStep({ email }) {
  const { linkGoogle, cancelStep } = useAuth()
  const toast = useToast()
  const headingRef = useRef(null)
  const [password, setPassword] = useState('')
  const [error, setError] = useState(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    headingRef.current?.focus()
  }, [])

  async function handleSubmit(event) {
    event.preventDefault()
    if (!password) return setError('Enter your PennyWise password.')
    setError(null)
    setBusy(true)
    try {
      const me = await linkGoogle(password)
      if (me) toast.success('Google is connected. Next time, Continue with Google signs you straight in.')
    } catch (err) {
      setBusy(false)
      if (err.status === 401) {
        cancelStep()
        return
      }
      setError(err.fieldErrors?.password ?? err.message)
    }
  }

  return (
    <>
      <h1 ref={headingRef} tabIndex={-1} className="auth__title">
        Connect Google
      </h1>
      <p className="auth__subtitle">
        {email} already has a PennyWise account. Enter its password once to connect Google to it.
      </p>

      <form className="auth__form" onSubmit={handleSubmit} noValidate>
        <PasswordField label="PennyWise password" value={password} onChange={(e) => { setPassword(e.target.value); setError(null) }}
          error={error} autoComplete="current-password" autoFocus />
        <button type="submit" className="button button--primary button--block" disabled={busy}>
          {busy ? 'Connecting…' : 'Connect and log in'}
        </button>
      </form>

      <p className="auth__switch">
        <button type="button" className="button button--link" onClick={cancelStep}>
          Back to log in
        </button>
      </p>
    </>
  )
}
