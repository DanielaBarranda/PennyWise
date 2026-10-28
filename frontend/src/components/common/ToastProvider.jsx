import { useCallback, useMemo, useRef, useState } from 'react'
import Icon from './Icon.jsx'
import { ToastContext } from '../../hooks/useToast.js'

const DISMISS_AFTER_MS = 6000
const TOAST_ICONS = { success: 'check', warning: 'alert', error: 'alert' }

export default function ToastProvider({ children }) {
  const [toasts, setToasts] = useState([])
  const nextId = useRef(1)

  const dismiss = useCallback((id) => {
    setToasts((list) => list.filter((t) => t.id !== id))
  }, [])

  const show = useCallback(
    (type, message) => {
      const id = nextId.current++
      setToasts((list) => [...list, { id, type, message }])
      setTimeout(() => dismiss(id), DISMISS_AFTER_MS)
    },
    [dismiss],
  )

  const value = useMemo(
    () => ({
      success: (message) => show('success', message),
      warning: (message) => show('warning', message),
      error: (message) => show('error', message),
    }),
    [show],
  )

  const renderList = (types) =>
    toasts
      .filter((t) => types.includes(t.type))
      .map((t) => (
        <div key={t.id} className={`toast toast--${t.type}`}>
          <span className="toast__icon" aria-hidden="true">
            <Icon name={TOAST_ICONS[t.type]} size={16} />
          </span>
          <span className="toast__message">{t.message}</span>
          <button type="button" className="toast__close" onClick={() => dismiss(t.id)} aria-label="Dismiss message">
            <Icon name="close" size={16} />
          </button>
        </div>
      ))

  return (
    <ToastContext.Provider value={value}>
      {children}
      {/* The live regions are always in the page so screen readers announce new messages.
          Successes are polite; warnings and errors interrupt. */}
      <div className="toast-stack">
        <div role="status" aria-live="polite">
          {renderList(['success'])}
        </div>
        <div role="alert" aria-live="assertive">
          {renderList(['warning', 'error'])}
        </div>
      </div>
    </ToastContext.Provider>
  )
}
