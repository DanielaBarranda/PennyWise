import Icon from '../common/Icon.jsx'
import { formatPeso } from '../../utils/format.js'

// Warnings for categories near or over their limit. The overall budget status is
// already shown in the overview panel, so it is not repeated here.
export default function BudgetAlerts({ summary }) {
  const alerts = summary.categories
    .filter((c) => c.status === 'Exceeded' || c.status === 'Warning')
    .map((c) =>
      c.status === 'Exceeded'
        ? { tone: 'danger', text: `${c.category} is over its ${formatPeso(c.limit)} limit by ${formatPeso(c.total - c.limit)}.` }
        : { tone: 'warning', text: `${c.category} is close to its ${formatPeso(c.limit)} limit. ${formatPeso(c.limit - c.total)} left.` },
    )

  if (alerts.length === 0) return null

  return (
    <ul className="alert-list" aria-label="Category warnings">
      {alerts.map((alert) => (
        <li key={alert.text} className={`alert alert--${alert.tone}`}>
          <Icon name="alert" size={18} />
          {alert.text}
        </li>
      ))}
    </ul>
  )
}
