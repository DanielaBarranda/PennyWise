import { formatPercent, formatPeso } from '../../utils/format.js'

function StatCard({ label, value, note, tone = 'default' }) {
  return (
    <div className={`stat-card stat-card--${tone}`}>
      <dt className="stat-card__label">{label}</dt>
      <dd className="stat-card__value">{value}</dd>
      {note && <dd className="stat-card__note">{note}</dd>}
    </div>
  )
}

// All values come straight from GET /api/dashboard/summary.
export default function SummaryCards({ summary }) {
  const hasBudget = summary.budget !== null
  const overBudget = hasBudget && summary.remaining < 0

  return (
    <dl className="stat-grid">
      <StatCard label="Monthly budget" value={hasBudget ? formatPeso(summary.budget.amount) : '—'} note={hasBudget ? null : 'Not set yet'} />
      <StatCard
        label="Total spent"
        value={formatPeso(summary.totalSpent)}
        note={`${summary.expenseCount} ${summary.expenseCount === 1 ? 'expense' : 'expenses'}`}
      />
      <StatCard
        label={overBudget ? 'Over budget by' : 'Remaining'}
        value={hasBudget ? formatPeso(Math.abs(summary.remaining)) : '—'}
        tone={overBudget ? 'danger' : 'positive'}
      />
      <StatCard
        label="Budget spent"
        value={hasBudget ? formatPercent(summary.percentSpent) : '—'}
        tone={summary.status === 'Exceeded' ? 'danger' : summary.status === 'Warning' ? 'warning' : 'default'}
      />
    </dl>
  )
}
