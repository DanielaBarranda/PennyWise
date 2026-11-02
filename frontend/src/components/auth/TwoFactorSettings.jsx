import { useEffect, useState } from 'react'
import FormField from '../common/FormField.jsx'
import Icon from '../common/Icon.jsx'
import Modal from '../common/Modal.jsx'
import QrCode from '../common/QrCode.jsx'
import { LoadingState } from '../common/States.jsx'
import { useAuth } from '../../hooks/useAuth.js'
import { useToast } from '../../hooks/useToast.js'
import { api } from '../../services/api.js'

// Profile → Sign-in and security: how this account signs in, and two-step
// verification with an authenticator app (Google Authenticator, Microsoft
// Authenticator, and others).
export default function TwoFactorSettings() {
  const { user } = useAuth()
  const [dialog, setDialog] = useState(null) // 'enable' | 'disable' | 'codes'

  return (
    <>
      <ul className="signin-methods">
        <li>
          <Icon name="lock" size={18} />
          <span className="signin-methods__name">Password</span>
          <span className="signin-methods__status">{user.hasPassword ? 'Set' : 'None. You sign in with Google.'}</span>
        </li>
        <li>
          <GoogleMark />
          <span className="signin-methods__name">Google</span>
          <span className="signin-methods__status">
            {user.googleConnected ? 'Connected' : 'Not connected. Use Continue with Google when you log in to connect it.'}
          </span>
        </li>
      </ul>

      <div className="two-factor">
        <div className="two-factor__head">
          <h3>Two-step verification</h3>
          <span className={`badge ${user.twoFactorEnabled ? 'badge--on' : 'badge--off'}`}>{user.twoFactorEnabled ? 'On' : 'Off'}</span>
        </div>
        {user.twoFactorEnabled ? (
          <>
            <p className="profile__hint">
              Logging in needs a code from your authenticator app as well as your {user.hasPassword ? 'password' : 'Google account'}.
              {' '}You have {user.recoveryCodesLeft} unused recovery code{user.recoveryCodesLeft === 1 ? '' : 's'}.
            </p>
            {user.recoveryCodesLeft <= 3 && (
              <p className="allowance__warning">You’re running low on recovery codes. Make new ones so you don’t get locked out.</p>
            )}
            <div className="profile__actions profile__actions--start">
              <button type="button" className="button button--secondary" onClick={() => setDialog('codes')}>
                New recovery codes
              </button>
              <button type="button" className="button button--ghost" onClick={() => setDialog('disable')}>
                Turn off
              </button>
            </div>
          </>
        ) : (
          <>
            <p className="profile__hint">
              Even if someone learns your {user.hasPassword ? 'password' : 'Google password'}, they can’t get in without the
              6-digit code from your phone. You need a free authenticator app such as Google Authenticator or Microsoft Authenticator.
            </p>
            <div className="profile__actions profile__actions--start">
              <button type="button" className="button button--primary" onClick={() => setDialog('enable')}>
                <Icon name="lock" />
                Turn on
              </button>
            </div>
          </>
        )}
      </div>

      <Modal open={dialog === 'enable'} title="Turn on two-step verification" onClose={() => setDialog(null)}>
        {dialog === 'enable' && <EnableFlow onDone={() => setDialog(null)} />}
      </Modal>
      <Modal open={dialog === 'disable'} title="Turn off two-step verification?"
        description="Logging in will only need your password (or Google) again." onClose={() => setDialog(null)}>
        {dialog === 'disable' && <DisableForm onDone={() => setDialog(null)} />}
      </Modal>
      <Modal open={dialog === 'codes'} title="New recovery codes" description="Your old recovery codes will stop working."
        onClose={() => setDialog(null)}>
        {dialog === 'codes' && <NewCodesFlow onDone={() => setDialog(null)} />}
      </Modal>
    </>
  )
}

// Scan the QR code, enter a code to prove it worked, then save the recovery codes.
function EnableFlow({ onDone }) {
  const { enableTwoFactor } = useAuth()
  const toast = useToast()
  const [setup, setSetup] = useState(null)
  const [setupError, setSetupError] = useState(null)
  const [codes, setCodes] = useState(null)

  useEffect(() => {
    let active = true
    api.twoFactorSetup().then(
      (s) => active && setSetup(s),
      (err) => active && setSetupError(err.message),
    )
    return () => {
      active = false
    }
  }, [])

  if (codes) {
    return (
      <RecoveryCodes
        codes={codes}
        intro="Two-step verification is on. If you lose your phone, each of these codes lets you log in once. Save them somewhere safe, like a password manager or a printed copy."
        onDone={onDone}
      />
    )
  }
  if (setupError) {
    return (
      <div className="form">
        <p className="form__error" role="alert"><Icon name="alert" size={18} />{setupError}</p>
      </div>
    )
  }
  if (!setup) {
    return (
      <div className="form">
        <LoadingState label="Preparing your setup code…" variant="rows" />
      </div>
    )
  }

  const groupedKey = setup.key.match(/.{1,4}/g).join(' ')
  return (
    <div className="form">
      <ol className="setup-steps">
        <li>Open your authenticator app and add an account (usually a <strong>+</strong> button).</li>
        <li>Scan this QR code with it.</li>
      </ol>
      <div className="setup-qr">
        <QrCode text={setup.uri} label="QR code for setting up PennyWise in an authenticator app" />
        <p className="setup-qr__key">
          Can’t scan it? Enter this key instead:
          <code>{groupedKey}</code>
        </p>
      </div>
      <CodeForm
        nested
        label="Then enter the 6-digit code the app shows"
        submitLabel="Turn on"
        busyLabel="Turning on…"
        onCancel={onDone}
        onSubmit={async (code) => {
          const recovery = await enableTwoFactor(code)
          toast.success('Two-step verification is on. Other devices have been logged out.')
          setCodes(recovery)
        }}
      />
    </div>
  )
}

function DisableForm({ onDone }) {
  const { disableTwoFactor } = useAuth()
  const toast = useToast()
  return (
    <CodeForm
      label="Code from your authenticator app, or a recovery code"
      allowRecovery
      submitLabel="Turn off"
      busyLabel="Turning off…"
      danger
      onCancel={onDone}
      onSubmit={async (code) => {
        await disableTwoFactor(code)
        toast.success('Two-step verification is off.')
        onDone()
      }}
    />
  )
}

function NewCodesFlow({ onDone }) {
  const { newRecoveryCodes } = useAuth()
  const [codes, setCodes] = useState(null)
  if (codes) {
    return <RecoveryCodes codes={codes} intro="Your old recovery codes no longer work. Save these instead." onDone={onDone} />
  }
  return (
    <CodeForm
      label="Code from your authenticator app"
      allowRecovery
      submitLabel="Make new codes"
      busyLabel="Making codes…"
      onCancel={onDone}
      onSubmit={async (code) => setCodes(await newRecoveryCodes(code))}
    />
  )
}

// `nested` when it sits inside another padded .form (the setup steps).
function CodeForm({ label, submitLabel, busyLabel, onSubmit, onCancel, allowRecovery = false, danger = false, nested = false }) {
  const [code, setCode] = useState('')
  const [error, setError] = useState(null)
  const [busy, setBusy] = useState(false)

  async function handleSubmit(event) {
    event.preventDefault()
    const cleaned = code.replace(/[\s-]/g, '')
    const valid = /^\d{6}$/.test(cleaned) || (allowRecovery && cleaned.length === 10)
    if (!valid) return setError(allowRecovery ? 'Enter the 6-digit code or a recovery code.' : 'Enter the 6-digit code from the app.')
    setError(null)
    setBusy(true)
    try {
      await onSubmit(code)
    } catch (err) {
      setError(err.fieldErrors?.code ?? err.message)
      setBusy(false)
    }
  }

  return (
    <form className={`form${nested ? ' form--nested' : ''}`} onSubmit={handleSubmit} noValidate>
      <FormField label={label} error={error}>
        {(props) => (
          <input {...props} className={allowRecovery ? undefined : 'code-input'} type="text"
            inputMode={allowRecovery ? 'text' : 'numeric'} autoComplete="one-time-code" autoCapitalize="none" spellCheck={false}
            maxLength={20} placeholder="123456" value={code}
            onChange={(e) => { setCode(e.target.value); setError(null) }} data-autofocus />
        )}
      </FormField>
      <div className="modal__actions">
        <button type="button" className="button button--ghost" onClick={onCancel} disabled={busy}>
          Cancel
        </button>
        <button type="submit" className={`button ${danger ? 'button--danger' : 'button--primary'}`} disabled={busy}>
          {busy ? busyLabel : submitLabel}
        </button>
      </div>
    </form>
  )
}

// Shown once. Copy and download are offered so the codes are not lost.
function RecoveryCodes({ codes, intro, onDone }) {
  const toast = useToast()
  const text = `PennyWise recovery codes\nEach code works once.\n\n${codes.join('\n')}\n`

  async function copy() {
    try {
      await navigator.clipboard.writeText(text)
      toast.success('Recovery codes copied.')
    } catch {
      toast.error('Copying is blocked in this browser. Download them or write them down instead.')
    }
  }

  function download() {
    const url = URL.createObjectURL(new Blob([text], { type: 'text/plain' }))
    const link = Object.assign(document.createElement('a'), { href: url, download: 'pennywise-recovery-codes.txt' })
    link.click()
    URL.revokeObjectURL(url)
  }

  return (
    <div className="form">
      <p className="profile__hint">{intro}</p>
      <ol className="recovery-codes" aria-label="Recovery codes">
        {codes.map((c) => (
          <li key={c}><code>{c}</code></li>
        ))}
      </ol>
      <div className="profile__actions profile__actions--start">
        <button type="button" className="button button--secondary button--small" onClick={copy}>Copy</button>
        <button type="button" className="button button--secondary button--small" onClick={download}>
          <Icon name="download" size={16} />
          Download
        </button>
      </div>
      <div className="modal__actions">
        <button type="button" className="button button--primary" onClick={onDone} data-autofocus>
          I’ve saved them
        </button>
      </div>
    </div>
  )
}

// Google's "G", for the sign-in methods list.
function GoogleMark() {
  return (
    <svg width="18" height="18" viewBox="0 0 48 48" aria-hidden="true">
      <path fill="#FFC107" d="M43.6 20.5H42V20H24v8h11.3C33.7 32.7 29.2 36 24 36c-6.6 0-12-5.4-12-12s5.4-12 12-12c3.1 0 5.8 1.2 7.9 3.1l5.7-5.7C34 6.1 29.3 4 24 4 12.9 4 4 12.9 4 24s8.9 20 20 20 20-8.9 20-20c0-1.3-.1-2.4-.4-3.5z" />
      <path fill="#FF3D00" d="M6.3 14.7l6.6 4.8C14.7 15.1 19 12 24 12c3.1 0 5.8 1.2 7.9 3.1l5.7-5.7C34 6.1 29.3 4 24 4 16.3 4 9.7 8.3 6.3 14.7z" />
      <path fill="#4CAF50" d="M24 44c5.2 0 9.9-2 13.4-5.2l-6.2-5.2C29.2 35.1 26.7 36 24 36c-5.2 0-9.6-3.3-11.3-8l-6.5 5C9.5 39.6 16.2 44 24 44z" />
      <path fill="#1976D2" d="M43.6 20.5H42V20H24v8h11.3c-.8 2.2-2.2 4.2-4.1 5.6l6.2 5.2C37 39.2 44 34 44 24c0-1.3-.1-2.4-.4-3.5z" />
    </svg>
  )
}
