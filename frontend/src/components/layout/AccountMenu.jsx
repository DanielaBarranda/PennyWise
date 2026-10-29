import { useEffect, useId, useRef, useState } from 'react'
import Icon from '../common/Icon.jsx'
import { useAuth } from '../../hooks/useAuth.js'
import { initials } from '../../utils/initials.js'

// The round avatar button in the top bar. It opens a small menu with the
// signed-in account, Profile and Log out.
// Keyboard: Enter/Space/Arrow Down opens it and focuses the first item, arrow
// keys move between items, Escape closes it and returns focus to the button.
export default function AccountMenu() {
  const { user, logout } = useAuth()
  const [open, setOpen] = useState(false)
  const [busy, setBusy] = useState(false)
  const rootRef = useRef(null)
  const buttonRef = useRef(null)
  const menuId = useId()
  const buttonId = useId()

  const items = () => [...(rootRef.current?.querySelectorAll('[role="menuitem"]') ?? [])]

  useEffect(() => {
    if (!open) return
    const onPointerDown = (event) => !rootRef.current?.contains(event.target) && setOpen(false)
    document.addEventListener('pointerdown', onPointerDown)
    return () => document.removeEventListener('pointerdown', onPointerDown)
  }, [open])

  function openMenu() {
    setOpen(true)
    requestAnimationFrame(() => items()[0]?.focus())
  }

  function close({ refocus = true } = {}) {
    setOpen(false)
    if (refocus) buttonRef.current?.focus()
  }

  function onButtonKeyDown(event) {
    if (event.key === 'ArrowDown') {
      event.preventDefault()
      openMenu()
    }
  }

  function onMenuKeyDown(event) {
    const list = items()
    const index = list.indexOf(document.activeElement)
    if (event.key === 'Escape') {
      event.preventDefault()
      close()
    } else if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault()
      const step = event.key === 'ArrowDown' ? 1 : -1
      list[(index + step + list.length) % list.length]?.focus()
    } else if (event.key === 'Home' || event.key === 'End') {
      event.preventDefault()
      list[event.key === 'Home' ? 0 : list.length - 1]?.focus()
    } else if (event.key === 'Tab') {
      close({ refocus: false })
    }
  }

  async function handleLogout() {
    setBusy(true)
    await logout()
  }

  return (
    <div className="account-menu" ref={rootRef}>
      <button
        ref={buttonRef}
        id={buttonId}
        type="button"
        className="account-menu__button"
        aria-haspopup="menu"
        aria-expanded={open}
        aria-controls={open ? menuId : undefined}
        aria-label={`Account menu for ${user.name}`}
        onClick={() => (open ? close() : openMenu())}
        onKeyDown={onButtonKeyDown}
      >
        <span className="avatar" aria-hidden="true">
          {initials(user.name)}
        </span>
      </button>

      {open && (
        <div className="account-menu__panel" onKeyDown={onMenuKeyDown}>
          <div className="account-menu__header">
            <span className="avatar avatar--lg" aria-hidden="true">
              {initials(user.name)}
            </span>
            <span className="account-menu__who">
              <span className="account-menu__name">{user.name}</span>
              <span className="account-menu__email">{user.email}</span>
            </span>
          </div>
          <div id={menuId} role="menu" aria-labelledby={buttonId} className="account-menu__items">
            <a href="#/profile" role="menuitem" className="account-menu__item" onClick={() => close({ refocus: false })}>
              <Icon name="user" size={18} />
              Profile
            </a>
            <button type="button" role="menuitem" className="account-menu__item account-menu__item--danger" onClick={handleLogout} disabled={busy}>
              <Icon name="logout" size={18} />
              {busy ? 'Logging out…' : 'Log out'}
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
