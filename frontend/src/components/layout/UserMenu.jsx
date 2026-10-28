import { useState } from 'react'
import Icon from '../common/Icon.jsx'
import { useAuth } from '../../hooks/useAuth.js'

const initials = (name) =>
  name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0].toUpperCase())
    .join('')

// The signed-in account and a Log out button. Used in the desktop top bar and
// in the phone menu panel (variant="panel").
export default function UserMenu({ variant = 'bar' }) {
  const { user, logout } = useAuth()
  const [busy, setBusy] = useState(false)

  async function handleLogout() {
    setBusy(true)
    await logout()
  }

  return (
    <div className={`user-menu user-menu--${variant}`}>
      <span className="user-menu__avatar" aria-hidden="true">
        {initials(user.name) || '?'}
      </span>
      <span className="user-menu__text">
        <span className="user-menu__name">{user.name}</span>
        <span className="user-menu__email">{user.email}</span>
      </span>
      <button type="button" className="button button--ghost button--small user-menu__logout" onClick={handleLogout} disabled={busy}>
        <Icon name="logout" size={16} />
        {busy ? 'Logging out…' : 'Log out'}
      </button>
    </div>
  )
}
