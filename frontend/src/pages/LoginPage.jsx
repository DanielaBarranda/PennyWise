import { useEffect, useRef, useState } from 'react'
import FormField from '../components/common/FormField.jsx'
import Icon from '../components/common/Icon.jsx'
import PasswordField from '../components/auth/PasswordField.jsx'
import { useAuth } from '../hooks/useAuth.js'
import { useToast } from '../hooks/useToast.js'
import { validateLogin } from '../utils/authValidation.js'

export default function LoginPage() {
  const { login, notice, clearNotice } = useAuth()
  const toast = useToast()
  const formRef = useRef(null)
  const headingRef = useRef(null)
  const [form, setForm] = useState({ email: '', password: '' })
  const [errors, setErrors] = useState({})
  const [formError, setFormError] = useState(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    document.title = 'Log in · PennyWise'
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
      toast.success(`Welcome back, ${me.name}.`)
    } catch (error) {
      setBusy(false)
      setFormError(error.message)
      if (Object.keys(error.fieldErrors ?? {}).length > 0) showErrors(error.fieldErrors)
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
