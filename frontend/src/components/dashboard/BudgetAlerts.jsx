import Icon from '../common/Icon.jsx'

const list = (names) => (names.length <= 2 ? names.join(' and ') : `${names.slice(0, -1).join(', ')} and ${names.at(-1)}`)

// One line summing up category warnings. The details (how much over, how much
// left) sit on each category row below, so they are not repeated here.
export default function BudgetAlerts({ summary }) {
  const over = summary.categories.filter((c) => c.status === 'Exceeded').map((c) => c.category)
  const close = summary.categories.filter((c) => c.status === 'Warning').map((c) => c.category)
  if (over.length === 0 && close.length === 0) return null

  const parts = []
  if (over.length) parts.push(`${list(over)} ${over.length === 1 ? 'is' : 'are'} over the limit`)
  if (close.length) parts.push(`${list(close)} ${close.length === 1 ? 'is' : 'are'} close to the limit`)

  return (
    <p className={`alert alert--${over.length ? 'danger' : 'warning'}`} role="note">
      <Icon name="alert" size={18} />
      <span>
        {parts.join('; ')}. See Spending by category for details.
      </span>
    </p>
  )
}
