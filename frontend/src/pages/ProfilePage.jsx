import { useRef, useState } from 'react'
import FormField from '../components/common/FormField.jsx'
import Avatar from '../components/common/Avatar.jsx'
import ConfirmDialog from '../components/common/ConfirmDialog.jsx'
import Modal from '../components/common/Modal.jsx'
import Icon from '../components/common/Icon.jsx'
import PageHeader from '../components/common/PageHeader.jsx'
import PasswordField from '../components/auth/PasswordField.jsx'
import PasswordRules from '../components/auth/PasswordRules.jsx'
import { useAuth } from '../hooks/useAuth.js'
import { useToast } from '../hooks/useToast.js'
import { PASSWORD_RULES } from '../utils/authValidation.js'
import { formatDate } from '../utils/format.js'
import { resizeAvatar } from '../utils/resizeImage.js'

export default function ProfilePage() {
  const { user, logout } = useAuth()

  return (
    <>
      <PageHeader title="Profile" subtitle="Your account details, preferences and password." />
      <div className="profile">
        <div className="profile__column">
          <section className="card" aria-labelledby="account-heading">
            <div className="profile__identity">
              <Avatar user={user} size="xl" />
              <div>
                <h2 id="account-heading">{user.name}</h2>
                <p className="profile__meta">{user.email}</p>
                {user.memberSince && <p className="profile__meta">Member since {formatDate(user.memberSince)}</p>}
                <PhotoControls />
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
        <div className="profile__column">
          <section className="card" aria-labelledby="preferences-heading">
            <div className="card__header">
              <h2 id="preferences-heading">Preferences</h2>
            </div>
            <AllowancePreference />
          </section>

          <section className="card profile__danger" aria-labelledby="delete-heading">
            <div className="card__header">
              <h2 id="delete-heading">Delete account</h2>
            </div>
            <DeleteAccount />
          </section>
        </div>
      </div>
    </>
  )
}

// Change or remove the profile photo. The photo is cropped to a square and
// shrunk in the browser, so only a small image is ever uploaded.
function PhotoControls() {
  const { user, uploadAvatar, removeAvatar } = useAuth()
  const toast = useToast()
  const inputRef = useRef(null)
  const [busy, setBusy] = useState(false)
  const [confirming, setConfirming] = useState(false)
  const [error, setError] = useState(null)
  const hasPhoto = Boolean(user.avatarVersion)

  async function handleFile(event) {
    const file = event.target.files?.[0]
    event.target.value = '' // choosing the same file again still triggers a change
    if (!file) return
    setError(null)
    setBusy(true)
    try {
      await uploadAvatar(await resizeAvatar(file))
      toast.success(hasPhoto ? 'Profile photo changed.' : 'Profile photo added.')
    } catch (err) {
      setError(err.fieldErrors?.photo ?? err.message)
    } finally {
      setBusy(false)
    }
  }

  async function handleRemove() {
    setBusy(true)
    try {
      await removeAvatar()
      setConfirming(false)
      toast.success('Profile photo removed.')
    } catch (err) {
      setError(err.message)
      setConfirming(false)
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="profile__photo">
      <div className="profile__photo-actions">
        <input ref={inputRef} type="file" accept="image/jpeg,image/png,image/webp" className="visually-hidden" tabIndex={-1} onChange={handleFile} />
        <button type="button" className="button button--secondary button--small" onClick={() => inputRef.current.click()} disabled={busy}>
          <Icon name="camera" size={16} />
          {busy ? 'Saving…' : hasPhoto ? 'Change photo' : 'Add photo'}
        </button>
        {hasPhoto && (
          <button type="button" className="button button--ghost button--small profile__photo-remove" onClick={() => setConfirming(true)} disabled={busy}>
            <Icon name="trash" size={16} />
            Remove photo
          </button>
        )}
      </div>
      {error ? (
        <p className="field__error" role="alert">{error}</p>
      ) : (
        <p className="profile__photo-hint">JPEG, PNG or WebP. It’s cropped to a square.</p>
      )}
      <ConfirmDialog
        open={confirming}
        title="Remove your profile photo?"
        message="Your initials will show instead. You can add a photo again at any time."
        confirmLabel="Remove photo"
        busy={busy}
        onConfirm={handleRemove}
        onCancel={() => setConfirming(false)}
      />
    </div>
  )
}

// Permanently deletes the account after the password is entered again and the
// consequences are acknowledged.
function DeleteAccount() {
  const [open, setOpen] = useState(false)
  const [busy, setBusy] = useState(false)

  return (
    <>
      <p className="profile__hint">
        Permanently deletes your account and everything in it: budgets, expenses, savings goals, bills and your photo.
        This can’t be undone. To keep a copy, download your monthly reports from{' '}
        <a href="#/reports">Reports</a> first.
      </p>
      <button type="button" className="button button--danger" onClick={() => setOpen(true)}>
        <Icon name="trash" />
        Delete my account
      </button>
      <Modal open={open} title="Delete your account?" description="This permanently removes your account and all of its data." onClose={busy ? () => {} : () => setOpen(false)}>
        {open && <DeleteAccountForm onCancel={() => setOpen(false)} onBusyChange={setBusy} />}
      </Modal>
    </>
  )
}

function DeleteAccountForm({ onCancel, onBusyChange }) {
  const { user, deleteAccount } = useAuth()
  const [password, setPassword] = useState('')
  const [understood, setUnderstood] = useState(false)
  const [error, setError] = useState(null)
  const [busy, setBusy] = useState(false)

  async function handleSubmit(event) {
    event.preventDefault()
    if (!password) return setError('Enter your password to confirm.')
    setError(null)
    setBusy(true)
    onBusyChange(true)
    try {
      await deleteAccount(password) // signs out and shows the login page
    } catch (err) {
      setError(err.fieldErrors?.password ?? err.message)
      setBusy(false)
      onBusyChange(false)
    }
  }

  return (
    <form className="form" onSubmit={handleSubmit} noValidate>
      <ul className="delete-list">
        <li>Your budgets, expenses, savings goals, bills and photo are deleted from PennyWise.</li>
        <li>You’re signed out on every device.</li>
        <li>You can sign up again with {user.email}, but you’ll start with an empty account.</li>
      </ul>
      <PasswordField
        label="Password"
        value={password}
        onChange={(e) => setPassword(e.target.value)}
        error={error}
        autoComplete="current-password"
        autoFocus
      />
      <label className="check">
        <input type="checkbox" checked={understood} onChange={(e) => setUnderstood(e.target.checked)} />
        <span>
          <span className="check__label">I understand this can’t be undone</span>
        </span>
      </label>
      <div className="modal__actions">
        <button type="button" className="button button--ghost" onClick={onCancel} disabled={busy}>
          Cancel
        </button>
        <button type="submit" className="button button--danger" disabled={!understood || busy}>
          {busy ? 'Deleting…' : 'Delete my account'}
        </button>
      </div>
    </form>
  )
}

const ALLOWANCE_OPTIONS = [
  { value: 'Daily', label: 'Daily', hint: 'What is left, divided by the days left in the month.' },
  { value: 'Weekly', label: 'Weekly', hint: 'The daily amount for the days left until Sunday.' },
  { value: 'Monthly', label: 'Monthly', hint: 'Everything left in this month’s budget.' },
]

// Saved as soon as it changes; the budget form and the dashboard's budget and
// allowance cards all use this setting.
function AllowancePreference() {
  const { user, updatePreferences } = useAuth()
  const toast = useToast()
  const [busy, setBusy] = useState(false)

  async function choose(value) {
    setBusy(true)
    try {
      await updatePreferences({ allowancePeriod: value })
      toast.success(`Your budget and allowance now show ${value.toLowerCase()}.`)
    } catch {
      toast.error('Your preference could not be saved. Please try again.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <fieldset className="choice-list" disabled={busy}>
      <legend className="choice-list__legend">Budget and allowance period</legend>
      {ALLOWANCE_OPTIONS.map((o) => (
        <label key={o.value} className="choice">
          <input type="radio" name="allowance-preference" value={o.value} checked={user.allowancePeriod === o.value} onChange={() => choose(o.value)} />
          <span>
            <span className="choice__label">{o.label}</span>
            <span className="choice__hint">{o.hint}</span>
          </span>
        </label>
      ))}
      <p className="profile__hint">
        Your budget is entered and shown in this period, and saved as a monthly amount, so reports stay monthly.
      </p>
    </fieldset>
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
