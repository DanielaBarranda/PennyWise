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

// Progress per category. With a limit, the bar shows how much of the limit is
// used; without one, it shows the category's share of the month's spending.
// Every value on a bar is also written out as text.
// `headingRef` lets the summary card's "View details" button move focus here.
export default function CategoryBreakdown({ summary, headingRef }) {
  const rows = summary.categories
    .filter((c) => c.total > 0 || c.limit !== null)
    .sort((a, b) => b.total - a.total)

  return (
    <section className="card" aria-labelledby="breakdown-heading">
      <div className="card__header">
        <h2 id="breakdown-heading" ref={headingRef} tabIndex={-1}>
          Category progress
        </h2>
      </div>

      {rows.length === 0 ? (
        <EmptyState compact title="Nothing to track yet" message="Set category limits or add expenses to see progress here." />
      ) : (
        <ul className="progress-list">
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

  return (
    <li className={`progress-row progress-row--${tone}`}>
      <CategoryIcon category={row.category} shape="round" />
      <div className="progress-row__body">
        <div className="progress-row__line">
          <span className="progress-row__name">
            {row.category}
            {badge && (
              <span className={`status-badge status-badge--${badge.tone}`}>
                <Icon name="alert" size={12} />
                {badge.text}
              </span>
            )}
          </span>
          <span className="progress-row__amounts">
            {formatPeso(row.total)}
            {hasLimit ? ` / ${formatPeso(row.limit)}` : ''}
          </span>
        </div>
        <div className="progress-row__track" aria-hidden="true">
          {row.total > 0 && (
            <span
              className="progress-row__bar"
              style={{ width: `${Math.min(percent, 100)}%`, '--cat': CATEGORY_COLORS[row.category] }}
            />
          )}
        </div>
        <p className="progress-row__detail">
          {!hasLimit
            ? 'No limit set'
            : row.status === 'Exceeded'
              ? `Over by ${formatPeso(row.total - row.limit)}`
              : `${formatPeso(row.limit - row.total)} left`}
        </p>
      </div>
      <span className="progress-row__percent">
        {formatPercent(percent)}
        <span className="visually-hidden">{hasLimit ? ' of the limit used' : ' of spending'}</span>
      </span>
    </li>
  )
}
