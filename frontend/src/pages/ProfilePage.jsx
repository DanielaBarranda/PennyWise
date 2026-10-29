import { useRef, useState } from 'react'
import FormField from '../components/common/FormField.jsx'
import Icon from '../components/common/Icon.jsx'
import PageHeader from '../components/common/PageHeader.jsx'
import PasswordField from '../components/auth/PasswordField.jsx'
import PasswordRules from '../components/auth/PasswordRules.jsx'
import { useAuth } from '../hooks/useAuth.js'
import { useToast } from '../hooks/useToast.js'
import { PASSWORD_RULES } from '../utils/authValidation.js'
import { formatDate } from '../utils/format.js'
import { initials } from '../utils/initials.js'

export default function ProfilePage() {
  const { user, logout } = useAuth()

  return (
    <>
      <PageHeader title="Profile" subtitle="Your account details and password." />
      <div className="profile">
        <section className="card" aria-labelledby="account-heading">
          <div className="profile__identity">
            <span className="avatar avatar--xl" aria-hidden="true">
              {initials(user.name)}
            </span>
            <div>
              <h2 id="account-heading">{user.name}</h2>
              <p className="profile__meta">{user.email}</p>
              {user.memberSince && <p className="profile__meta">Member since {formatDate(user.memberSince)}</p>}
            </div>
          </div>
          <NameForm />
          <div className="profile__signout">
            <button type="button" className="button button--secondary" onClick={logout}>
              <Icon name="logout" />
              Log out
            </button>
          </div>
        </section>

        <section className="card" aria-labelledby="password-heading">
          <div className="card__header">
            <h2 id="password-heading">Change password</h2>
          </div>
          <p className="profile__hint">For your security, other devices will be logged out after you change it.</p>
          <PasswordForm />
        </section>
      </div>
    </>
  )
}

function NameForm() {
  const { user, updateProfile } = useAuth()
  const toast = useToast()
  const [name, setName] = useState(user.name)
  const [error, setError] = useState(null)
  const [busy, setBusy] = useState(false)

  async function handleSubmit(event) {
    event.preventDefault()
    const trimmed = name.trim()
    if (!trimmed) return setError('Name is required.')
    if (trimmed.length > 80) return setError('Name must be 80 characters or fewer.')
    setBusy(true)
    try {
      await updateProfile({ name: trimmed })
      setError(null)
      toast.success('Name updated.')
    } catch (err) {
      setError(err.fieldErrors?.name ?? err.message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <form className="profile__form" onSubmit={handleSubmit} noValidate>
      <FormField label="Name" error={error}>
        {(props) => (
          <input
            {...props}
            type="text"
            autoComplete="name"
            maxLength={80}
            value={name}
            onChange={(e) => {
              setName(e.target.value)
              setError(null)
            }}
          />
        )}
      </FormField>
      <FormField label="Email" hint="Your email is how you log in, so it can’t be changed here.">
        {(props) => <input {...props} type="email" value={user.email} readOnly />}
      </FormField>
      <div className="profile__actions">
        <button type="submit" className="button button--primary" disabled={busy || name.trim() === user.name}>
          {busy ? 'Saving…' : 'Save name'}
        </button>
      </div>
    </form>
  )
}

const EMPTY = { currentPassword: '', newPassword: '', confirmPassword: '' }

function PasswordForm() {
  const { changePassword } = useAuth()
  const toast = useToast()
  const formRef = useRef(null)
  const [form, setForm] = useState(EMPTY)
  const [errors, setErrors] = useState({})
  const [formError, setFormError] = useState(null)
  const [busy, setBusy] = useState(false)

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
    const clientErrors = {}
    if (!form.currentPassword) clientErrors.currentPassword = 'Enter your current password.'
    if (!PASSWORD_RULES.every((r) => r.test(form.newPassword))) {
      clientErrors.newPassword = 'New password must be at least 8 characters and include a letter and a number.'
    }
    if (form.confirmPassword !== form.newPassword) clientErrors.confirmPassword = 'Passwords do not match.'
    if (Object.keys(clientErrors).length > 0) return showErrors(clientErrors)

    setBusy(true)
    try {
      await changePassword(form)
      setForm(EMPTY)
      setErrors({})
      toast.success('Password changed. Other devices have been logged out.')
    } catch (err) {
      if (Object.keys(err.fieldErrors ?? {}).length > 0) showErrors(err.fieldErrors)
      else setFormError(err.message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <form ref={formRef} className="profile__form" onSubmit={handleSubmit} noValidate>
      {formError && (
        <p className="form__error" role="alert">
          <Icon name="alert" size={18} />
          {formError}
        </p>
      )}
      <PasswordField label="Current password" value={form.currentPassword} onChange={update('currentPassword')}
        error={errors.currentPassword} autoComplete="current-password" />
      <div>
        <PasswordField label="New password" value={form.newPassword} onChange={update('newPassword')}
          error={errors.newPassword} autoComplete="new-password" />
        <PasswordRules password={form.newPassword} />
      </div>
      <PasswordField label="Confirm new password" value={form.confirmPassword} onChange={update('confirmPassword')}
        error={errors.confirmPassword} autoComplete="new-password" />
      <div className="profile__actions">
        <button type="submit" className="button button--primary" disabled={busy}>
          <Icon name="lock" />
          {busy ? 'Changing…' : 'Change password'}
        </button>
      </div>
    </form>
  )
}
