import { useState } from 'react'
import FormField from '../common/FormField.jsx'
import Icon from '../common/Icon.jsx'

// A password input with a show/hide button. The button says what it will do
// ("Show password" / "Hide password"), and aria-pressed tells screen readers
// whether the password is currently visible.
export default function PasswordField({ label, value, onChange, error, hint, autoComplete, autoFocus }) {
  const [visible, setVisible] = useState(false)

  return (
    <FormField label={label} error={error} hint={hint}>
      {(props) => (
        <div className="input-affix">
          <input
            {...props}
            type={visible ? 'text' : 'password'}
            autoComplete={autoComplete}
            value={value}
            onChange={onChange}
            data-autofocus={autoFocus || undefined}
            className="input-affix__input--suffix"
          />
          <button
            type="button"
            className="input-affix__suffix"
            onClick={() => setVisible((v) => !v)}
            aria-pressed={visible}
            aria-label={visible ? 'Hide password' : 'Show password'}
            title={visible ? 'Hide password' : 'Show password'}
          >
            <Icon name={visible ? 'eyeOff' : 'eye'} size={18} />
          </button>
        </div>
      )}
    </FormField>
  )
}
