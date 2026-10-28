import Icon from './Icon.jsx'

// Shared loading, empty and error states, so no section is ever just blank.

// Grey placeholder shapes in the layout's own outline, so the page does not jump
// when the data arrives. Screen readers hear the label instead.
export function LoadingState({ label = 'Loading…', variant = 'block' }) {
  return (
    <div className={`skeleton skeleton--${variant}`} role="status">
      <span className="visually-hidden">{label}</span>
      <span className="skeleton__bar skeleton__bar--wide" aria-hidden="true" />
      <span className="skeleton__bar" aria-hidden="true" />
      <span className="skeleton__bar skeleton__bar--short" aria-hidden="true" />
    </div>
  )
}

export function EmptyState({ title, message, action, compact = false }) {
  return (
    <div className={`state state--empty${compact ? ' state--compact' : ''}`}>
      <span className="state__coin" aria-hidden="true">
        ₱
      </span>
      <p className="state__title">{title}</p>
      {message && <p className="state__message">{message}</p>}
      {action}
    </div>
  )
}

export function ErrorState({ error, onRetry }) {
  return (
    <div className="state state--error" role="alert">
      <span className="state__error-icon" aria-hidden="true">
        <Icon name="alert" size={22} />
      </span>
      <p className="state__title">Couldn’t load your data</p>
      <p className="state__message">{error?.message ?? 'The data could not be loaded.'}</p>
      {onRetry && (
        <button type="button" className="button button--secondary" onClick={onRetry}>
          Try again
        </button>
      )}
    </div>
  )
}
