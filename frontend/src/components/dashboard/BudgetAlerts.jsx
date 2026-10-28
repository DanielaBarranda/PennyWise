import { formatPercent, formatPeso } from '../../utils/format.js'

// Warning banners for the whole budget and for each category over its limit.
export default function BudgetAlerts({ summary }) {
  const alerts = []

  if (summary.status === 'Exceeded') {
    alerts.push({
      tone: 'danger',
      text: `You are over your monthly budget by ${formatPeso(-summary.remaining)}.`,
    })
  } else if (summary.status === 'Warning') {
    alerts.push({
      tone: 'warning',
      text: `You have used ${formatPercent(summary.percentSpent)} of your monthly budget. ${formatPeso(summary.remaining)} left.`,
    })
  }

  for (const c of summary.categories) {
    if (c.status === 'Exceeded') {
      alerts.push({
        tone: 'danger',
        text: `${c.category} is over its ${formatPeso(c.limit)} limit by ${formatPeso(c.total - c.limit)}.`,
      })
    } else if (c.status === 'Warning') {
      alerts.push({ tone: 'warning', text: `${c.category} is close to its ${formatPeso(c.limit)} limit.` })
    }
  }

  if (alerts.length === 0) return null

  return (
    <ul className="alert-list" aria-label="Budget warnings">
      {alerts.map((alert) => (
        <li key={alert.text} className={`alert alert--${alert.tone}`}>
          <span className="alert__icon" aria-hidden="true">
            !
          </span>
          {alert.text}
        </li>
      ))}
    </ul>
  )
}
