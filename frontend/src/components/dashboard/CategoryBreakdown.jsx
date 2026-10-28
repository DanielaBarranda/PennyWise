import { CategoryIcon } from '../common/CategoryBadge.jsx'
import Icon from '../common/Icon.jsx'
import { EmptyState } from '../common/States.jsx'
import { CATEGORY_COLORS } from '../../utils/constants.js'
import { formatPercent, formatPeso } from '../../utils/format.js'

// The category status comes from the API; a written badge plus an icon makes
// the warning clear without relying on the bar's color.
const STATUS_BADGE = {
  Warning: { text: 'Close to limit', tone: 'warning' },
  Exceeded: { text: 'Over limit', tone: 'danger' },
}

// One row per category with spending or a limit. With a limit, the bar shows how
// much of that limit is used; without one, it shows the category's share of the
// month's spending. Every bar value is also written out as text.
export default function CategoryBreakdown({ summary }) {
  const rows = summary.categories
    .filter((c) => c.total > 0 || c.limit !== null)
    .sort((a, b) => b.total - a.total)

  return (
    <section className="card" aria-labelledby="breakdown-heading">
      <div className="card__header">
        <h2 id="breakdown-heading">Spending by category</h2>
      </div>

      {rows.length === 0 ? (
        <EmptyState compact title="Nothing spent yet" message="Each category’s total appears here once you add expenses." />
      ) : (
        <ul className="breakdown">
          {rows.map((c) => (
            <CategoryRow key={c.category} row={c} totalSpent={summary.totalSpent} />
          ))}
        </ul>
      )}
    </section>
  )
}

function CategoryRow({ row, totalSpent }) {
  const hasLimit = row.limit !== null
  const percent = hasLimit ? (row.total / row.limit) * 100 : totalSpent > 0 ? (row.total / totalSpent) * 100 : 0
  const badge = STATUS_BADGE[row.status]
  const tone = badge?.tone ?? 'ok'

  let detail
  if (!hasLimit) detail = 'No limit set'
  else if (row.status === 'Exceeded') detail = `Over by ${formatPeso(row.total - row.limit)} of ${formatPeso(row.limit)}`
  else detail = `${formatPeso(row.limit - row.total)} left of ${formatPeso(row.limit)}`

  return (
    <li className={`breakdown__row breakdown__row--${tone}`}>
      <CategoryIcon category={row.category} />
      <div className="breakdown__body">
        <div className="breakdown__line">
          <span className="breakdown__name">
            {row.category}
            {badge && (
              <span className={`status-badge status-badge--${badge.tone}`}>
                <Icon name="alert" size={12} />
                {badge.text}
              </span>
            )}
          </span>
          <span className="breakdown__amount">{formatPeso(row.total)}</span>
        </div>
        <div className="breakdown__track" aria-hidden="true">
          {row.total > 0 && (
            <span
              className="breakdown__bar"
              style={{ width: `${Math.min(percent, 100)}%`, '--cat': CATEGORY_COLORS[row.category] }}
            />
          )}
        </div>
        <div className="breakdown__line breakdown__meta">
          <span>{detail}</span>
          <span>{hasLimit ? `${formatPercent(percent)} used` : `${formatPercent(percent)} of spending`}</span>
        </div>
      </div>
    </li>
  )
}
