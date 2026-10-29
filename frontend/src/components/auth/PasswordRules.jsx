import Icon from '../common/Icon.jsx'
import { PASSWORD_RULES } from '../../utils/authValidation.js'

// Each rule turns into a check mark as it is met, so it is clear what is still missing.
export default function PasswordRules({ password }) {
  return (
    <ul className="password-rules" aria-label="Password requirements">
      {PASSWORD_RULES.map((rule) => {
        const met = rule.test(password)
        return (
          <li key={rule.id} className={met ? 'password-rules__met' : undefined}>
            <Icon name={met ? 'check' : 'dot'} size={14} />
            {rule.text}
            <span className="visually-hidden">{met ? ' (done)' : ' (not yet)'}</span>
          </li>
        )
      })}
    </ul>
  )
}
