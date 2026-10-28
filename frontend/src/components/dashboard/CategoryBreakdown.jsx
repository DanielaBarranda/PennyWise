import { EmptyState } from '../common/States.jsx'
import { CATEGORY_COLORS } from '../../utils/constants.js'
import { formatPercent, formatPeso } from '../../utils/format.js'

// A horizontal bar chart drawn with plain CSS. Every bar also shows its value
// as text, so the chart needs no separate screen-reader table and no chart library.
export default function CategoryBreakdown({ summary }) {
  const spent = summary.categories.filter((c) => c.total > 0).sort((a, b) => b.total - a.total)
  const largest = spent[0]?.total ?? 0

  return (
    <section className="card" aria-labelledby="breakdown-heading">
      <div className="card__header">
        <h2 id="breakdown-heading">Spending by category</h2>
      </div>

      {spent.length === 0 ? (
        <EmptyState title="No spending yet" message="Category totals will appear once you add expenses." />
      ) : (
        <ul className="breakdown">
          {spent.map((c) => {
            const share = summary.totalSpent > 0 ? (c.total / summary.totalSpent) * 100 : 0
            return (
              <li key={c.category} className="breakdown__item">
                <div className="breakdown__row">
                  <span className="breakdown__name">{c.category}</span>
                  <span className="breakdown__value amount">
                    {formatPeso(c.total)} <span className="breakdown__share">({formatPercent(share)})</span>
                  </span>
                </div>
                <div className="breakdown__track" aria-hidden="true">
                  <div
                    className="breakdown__bar"
                    style={{ width: `${(c.total / largest) * 100}%`, background: CATEGORY_COLORS[c.category] }}
                  />
                </div>
                {c.limit !== null && (
                  <p className={`breakdown__limit${c.status === 'Exceeded' ? ' breakdown__limit--over' : ''}`}>
                    Limit {formatPeso(c.limit)}
                    {c.status === 'Exceeded' ? ' · over limit' : c.status === 'Warning' ? ' · close to limit' : ''}
                  </p>
                )}
              </li>
            )
          })}
        </ul>
      )}
    </section>
  )
}
