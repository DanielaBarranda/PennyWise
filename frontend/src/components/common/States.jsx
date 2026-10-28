// Shared loading, empty and error states, so no section is ever just blank.

export function LoadingState({ label = 'Loading…' }) {
  return (
    <div className="state" role="status">
      <span className="spinner" aria-hidden="true" />
      <p>{label}</p>
    </div>
  )
}

export function EmptyState({ title, message, action }) {
  return (
    <div className="state state--empty">
      <svg className="state__icon" viewBox="0 0 48 48" aria-hidden="true">
        <circle cx="24" cy="24" r="20" fill="none" stroke="currentColor" strokeWidth="2.5" />
        <path d="M17 24h14M24 17v14" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      </svg>
      <p className="state__title">{title}</p>
      {message && <p className="state__message">{message}</p>}
      {action}
    </div>
  )
}

export function ErrorState({ error, onRetry }) {
  return (
    <div className="state state--error" role="alert">
      <p className="state__title">Something went wrong</p>
      <p className="state__message">{error?.message ?? 'The data could not be loaded.'}</p>
      {onRetry && (
        <button type="button" className="button button--secondary" onClick={onRetry}>
          Try again
        </button>
      )}
    </div>
  )
}
