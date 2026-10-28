import { formatPercent, formatPeso } from '../../utils/format.js'

const STATUS_TEXT = {
  OnTrack: 'On track',
  Warning: 'Approaching limit',
  Exceeded: 'Over budget',
}

export default function BudgetProgress({ summary }) {
  const percent = Number(summary.percentSpent ?? 0)
  const tone = summary.status === 'Exceeded' ? 'danger' : summary.status === 'Warning' ? 'warning' : 'ok'

  return (
    <section className="card" aria-labelledby="progress-heading">
      <div className="card__header">
        <h2 id="progress-heading">Spending progress</h2>
        <span className={`status-pill status-pill--${tone}`}>{STATUS_TEXT[summary.status]}</span>
      </div>
      <div
        className={`progress progress--${tone}`}
        role="progressbar"
        aria-labelledby="progress-heading"
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={Math.min(Math.round(percent), 100)}
        aria-valuetext={`${formatPercent(percent)} of budget spent`}
      >
        <div className="progress__fill" style={{ width: `${Math.min(percent, 100)}%` }} />
      </div>
      <p className="progress__caption">
        <span className="amount">{formatPeso(summary.totalSpent)}</span> of{' '}
        <span className="amount">{formatPeso(summary.budget.amount)}</span> spent
      </p>
    </section>
  )
}
