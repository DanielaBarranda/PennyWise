import Icon from '../common/Icon.jsx'

const ICONS = { Critical: 'alert', Warning: 'alert', Success: 'check', Info: 'info' }
const LINK_LABELS = { bills: 'View bills', savings: 'View savings', expenses: 'View expenses', dashboard: 'View dashboard' }

// A list of insights from GET /api/insights. Each one says whether it is a fact
// from your records or a suggestion, and non-urgent ones can be hidden.
export default function InsightList({ insights, onDismiss, showLinks = true }) {
  return (
    <ul className="insights">
      {insights.map((insight) => (
        <li key={insight.key} className={`insight insight--${insight.severity.toLowerCase()}`}>
          <span className="insight__icon" aria-hidden="true">
            <Icon name={ICONS[insight.severity]} size={16} />
          </span>
          <div className="insight__body">
            <p className="insight__title">
              {insight.title}
              <span className={`insight__kind insight__kind--${insight.kind.toLowerCase()}`}>
                {insight.kind === 'Suggestion' ? 'Suggestion' : 'From your records'}
              </span>
            </p>
            <p className="insight__message">{insight.message}</p>
            {showLinks && insight.link !== 'dashboard' && (
              <a className="button button--link insight__link" href={`#/${insight.link}`}>
                {LINK_LABELS[insight.link]}
              </a>
            )}
          </div>
          {insight.dismissible && onDismiss && (
            <button
              type="button"
              className="icon-button insight__dismiss"
              onClick={() => onDismiss(insight.key)}
              aria-label={`Hide: ${insight.title}`}
              title="Hide this insight"
            >
              <Icon name="close" size={16} />
            </button>
          )}
        </li>
      ))}
    </ul>
  )
}
