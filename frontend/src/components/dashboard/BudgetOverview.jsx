import Icon from '../common/Icon.jsx'
import { WARNING_PERCENT } from '../../utils/constants.js'
import { formatMonth, formatPercent, formatPeso } from '../../utils/format.js'

// The status always comes from the API (GET /api/dashboard/summary); this only
// chooses words and an icon for it, so the status is never shown by color alone.
const STATUS = {
  OnTrack: { text: 'On track', icon: 'check', tone: 'ok' },
  Warning: { text: 'Approaching limit', icon: 'alert', tone: 'warning' },
  Exceeded: { text: 'Budget exceeded', icon: 'alert', tone: 'danger' },
}

// The month at a glance: what is left to spend, the three supporting numbers,
// and progress toward the budget, in one burgundy panel.
export default function BudgetOverview({ summary, onSetBudget }) {
  const monthName = formatMonth(summary.month)
  const expenseCount = `${summary.expenseCount} ${summary.expenseCount === 1 ? 'expense' : 'expenses'}`

  if (!summary.budget) {
    return (
      <section className="overview overview--empty" aria-labelledby="overview-heading">
        <div className="overview__head">
          <h2 id="overview-heading" className="overview__label">
            No budget for {monthName}
          </h2>
          <p className="overview__figure">{formatPeso(summary.totalSpent)}</p>
          <p className="overview__caption">
            {summary.expenseCount === 0 ? 'Nothing recorded yet.' : `Spent so far across ${expenseCount}.`} Set a budget to see
            what is left and get warned before you overspend.
          </p>
        </div>
        <div className="overview__cta">
          <button type="button" className="button button--gold" onClick={onSetBudget}>
            <Icon name="wallet" />
            Set a budget
          </button>
        </div>
      </section>
    )
  }

  const status = STATUS[summary.status]
  const over = summary.remaining < 0
  const percent = Number(summary.percentSpent)

  return (
    <section className={`overview overview--${status.tone}`} aria-labelledby="overview-heading">
      <div className="overview__head">
        <div className="overview__title-row">
          <h2 id="overview-heading" className="overview__label">
            {over ? `Over budget for ${monthName} by` : `Left to spend in ${monthName}`}
          </h2>
          <span className={`status-badge status-badge--on-dark status-badge--${status.tone}`}>
            <Icon name={status.icon} size={14} />
            {status.text}
          </span>
        </div>
        <p className="overview__figure">{formatPeso(Math.abs(summary.remaining))}</p>
      </div>

      <dl className="overview__stats">
        <div className="overview__stat">
          <dt>Monthly budget</dt>
          <dd>{formatPeso(summary.budget.amount)}</dd>
        </div>
        <div className="overview__stat">
          <dt>Total spent</dt>
          <dd>{formatPeso(summary.totalSpent)}</dd>
          <dd className="overview__stat-note">{expenseCount}</dd>
        </div>
        <div className="overview__stat">
          <dt>Budget used</dt>
          <dd>{formatPercent(percent)}</dd>
        </div>
      </dl>

      <div className="overview__progress">
        <div
          className="meter"
          role="progressbar"
          aria-label="Budget used"
          aria-valuemin={0}
          aria-valuemax={100}
          aria-valuenow={Math.min(Math.round(percent), 100)}
          aria-valuetext={`${formatPercent(percent)} of the ${formatPeso(summary.budget.amount)} budget used`}
        >
          <div className="meter__fill" style={{ width: `${Math.min(percent, 100)}%` }} />
          {/* Where warnings start, so the bar shows how close the month is to them. */}
          <span className="meter__mark" style={{ left: `${WARNING_PERCENT}%` }} aria-hidden="true" />
        </div>
        <div className="overview__progress-labels">
          <span>
            {formatPeso(summary.totalSpent)} of {formatPeso(summary.budget.amount)} spent
          </span>
          <span className="overview__mark-label">
            <span className="overview__mark-key" aria-hidden="true" />
            Warnings start at {WARNING_PERCENT}%
          </span>
        </div>
      </div>
    </section>
  )
}
