import { useEffect, useRef, useState } from 'react'
import FormField from '../components/common/FormField.jsx'
import Icon from '../components/common/Icon.jsx'
import PasswordField from '../components/auth/PasswordField.jsx'
import { useAuth } from '../hooks/useAuth.js'
import { useToast } from '../hooks/useToast.js'
import { PASSWORD_RULES, validateRegistration } from '../utils/authValidation.js'

export default function RegisterPage() {
  const { register } = useAuth()
  const toast = useToast()
  const formRef = useRef(null)
  const headingRef = useRef(null)
  const [form, setForm] = useState({ name: '', email: '', password: '', confirmPassword: '' })
  const [errors, setErrors] = useState({})
  const [formError, setFormError] = useState(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    document.title = 'Create account · PennyWise'
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
    const clientErrors = validateRegistration(form)
    if (Object.keys(clientErrors).length > 0) return showErrors(clientErrors)

    setErrors({})
    setBusy(true)
    try {
      const me = await register({ ...form, name: form.name.trim(), email: form.email.trim() })
      toast.success(`Welcome to PennyWise, ${me.name}.`)
    } catch (error) {
      setBusy(false)
      setFormError(error.message)
      if (Object.keys(error.fieldErrors ?? {}).length > 0) showErrors(error.fieldErrors)
    }
  }

  const confirmMismatch = form.confirmPassword && form.confirmPassword !== form.password

  return (
    <>
      <h1 ref={headingRef} tabIndex={-1} className="auth__title">
        Create your account
      </h1>
      <p className="auth__subtitle">Your budget and expenses stay private to your account.</p>

      <form ref={formRef} className="auth__form" onSubmit={handleSubmit} noValidate>
        {formError && (
          <p className="form__error" role="alert">
            <Icon name="alert" size={18} />
            {formError}
          </p>
        )}

        <FormField label="Name" error={errors.name}>
          {(props) => <input {...props} type="text" autoComplete="name" maxLength={80} value={form.name} onChange={update('name')} />}
        </FormField>

        <FormField label="Email" error={errors.email}>
          {(props) => (
            <input {...props} type="email" autoComplete="email" inputMode="email" value={form.email} onChange={update('email')} />
          )}
        </FormField>

        <div>
          <PasswordField
            label="Password"
            value={form.password}
            onChange={update('password')}
            error={errors.password}
            autoComplete="new-password"
          />
          {/* Each rule turns into a check mark as it is met, so it is clear what is still missing. */}
          <ul className="password-rules" aria-label="Password requirements">
            {PASSWORD_RULES.map((rule) => {
              const met = rule.test(form.password)
              return (
                <li key={rule.id} className={met ? 'password-rules__met' : undefined}>
                  <Icon name={met ? 'check' : 'dot'} size={14} />
                  {rule.text}
                  <span className="visually-hidden">{met ? ' (done)' : ' (not yet)'}</span>
                </li>
              )
            })}
          </ul>
        </div>

        <PasswordField
          label="Confirm password"
          value={form.confirmPassword}
          onChange={update('confirmPassword')}
          error={errors.confirmPassword ?? (confirmMismatch ? 'Passwords do not match yet.' : undefined)}
          autoComplete="new-password"
        />

        <button type="submit" className="button button--primary button--block" disabled={busy}>
          {busy ? 'Creating account…' : 'Create account'}
        </button>
      </form>

      <p className="auth__switch">
        Already have an account? <a href="#/login">Log in</a>
      </p>
    </>
  )
}
