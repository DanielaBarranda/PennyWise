import Icon from '../common/Icon.jsx'
import { useAuth } from '../../hooks/useAuth.js'
import { PERIOD_WORDS, fromMonthly } from '../../utils/budgetPeriod.js'
import { formatMonth, formatPercent, formatPeso } from '../../utils/format.js'

// The overall status comes from the API; it is written out as well as colored.
const STATUS = {
  OnTrack: { text: 'On track', icon: 'check', tone: 'ok' },
  Warning: { text: 'Approaching limit', icon: 'alert', tone: 'warning' },
  Exceeded: { text: 'Budget exceeded', icon: 'alert', tone: 'danger' },
}

function StatCard({ icon, tone, label, value, children }) {
  return (
    <div className="stat">
      <span className={`stat__icon stat__icon--${tone}`} aria-hidden="true">
        <Icon name={icon} size={20} />
      </span>
      <dl className="stat__body">
        <dt className="stat__label">{label}</dt>
        <dd className="stat__value">{value}</dd>
        {children && <dd className="stat__note">{children}</dd>}
      </dl>
    </div>
  )
}

// Four cards: budget, spent, remaining, and what needs attention. Every number
// comes straight from GET /api/dashboard/summary.
export default function SummaryCards({ summary, onSetBudget, onShowCategories }) {
  const { user } = useAuth()
  const budget = summary.budget
  // Shown in the period the budget was set in (day, week or month); it is saved monthly.
  const period = user.allowancePeriod ?? 'Monthly'
  const budgetValue = !budget
    ? 'Not set'
    : period === 'Monthly'
      ? formatPeso(budget.amount)
      : (
          <>
            {formatPeso(fromMonthly(budget.amount, period, summary.month))}
            <span className="stat__unit"> {PERIOD_WORDS[period]}</span>
          </>
        )
  const status = budget ? STATUS[summary.status] : null
  const percent = Number(summary.percentSpent ?? 0)
  const over = budget && summary.remaining < 0
  const expenseCount = `${summary.expenseCount} ${summary.expenseCount === 1 ? 'expense' : 'expenses'}`
  const needsAttention = summary.categories.filter((c) => c.status === 'Exceeded' || c.status === 'Warning')
  const overCount = needsAttention.filter((c) => c.status === 'Exceeded').length

  return (
    <section className="stats" aria-label="Month summary">
      <StatCard icon="target" tone="primary" label="Total budget" value={budgetValue}>
        {!budget
          ? 'Set one to track what is left'
          : period === 'Monthly'
            ? 'For this month'
            : `${formatPeso(budget.amount)} for ${formatMonth(summary.month)}`}
      </StatCard>

      <StatCard icon="spent" tone="gold" label="Total spent" value={formatPeso(summary.totalSpent)}>
        {budget ? `${formatPercent(percent)} of budget, ${expenseCount}` : expenseCount}
      </StatCard>

      <StatCard
        icon="wallet"
        tone={over ? 'danger' : 'success'}
        label={over ? 'Over budget by' : 'Remaining balance'}
        value={budget ? formatPeso(Math.abs(summary.remaining)) : '—'}
      >
        {status ? (
          <span className={`status-badge status-badge--${status.tone}`}>
            <Icon name={status.icon} size={12} />
            {status.text}
          </span>
        ) : (
          'Needs a budget'
        )}
      </StatCard>

      {!budget ? (
        <div className="stat stat--action">
          <p className="stat__action-title">No budget for this month</p>
          <button type="button" className="button button--primary button--small" onClick={onSetBudget}>
            <Icon name="plus" />
            Set a budget
          </button>
        </div>
      ) : needsAttention.length > 0 ? (
        <div className={`stat stat--alert stat--alert-${overCount ? 'danger' : 'warning'}`}>
          <span className="stat__icon" aria-hidden="true">
            <Icon name="alert" size={20} />
          </span>
          <div className="stat__body">
            <p className="stat__alert-title">
              {needsAttention.length} {needsAttention.length === 1 ? 'category needs' : 'categories need'} attention
            </p>
            <p className="stat__note">
              {overCount > 0 ? `${overCount} over the limit` : 'Close to the limit'}
            </p>
            <button type="button" className="button button--link stat__link" onClick={onShowCategories}>
              View details
            </button>
          </div>
        </div>
      ) : (
        <StatCard icon="check" tone="success" label="Category limits" value="All on track">
          No category is near its limit
        </StatCard>
      )}
    </section>
  )
}
