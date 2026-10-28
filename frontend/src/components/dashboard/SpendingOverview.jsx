import { EmptyState } from '../common/States.jsx'
import { CATEGORY_COLORS } from '../../utils/constants.js'
import { formatPercent, formatPeso } from '../../utils/format.js'

const SIZE = 200
const STROKE = 26
const RADIUS = (SIZE - STROKE) / 2
const CIRCUMFERENCE = 2 * Math.PI * RADIUS
const GAP = 3 // px of empty ring between segments

// A donut of this month's spending by category, drawn with plain SVG. The legend
// beside it lists the same amounts and percentages as text, so the chart is
// decorative for screen readers.
export default function SpendingOverview({ summary }) {
  const slices = summary.categories.filter((c) => c.total > 0).sort((a, b) => b.total - a.total)
  const total = summary.totalSpent

  // Each segment starts where the previous one ended.
  const segments = slices.reduce((list, c) => {
    const previous = list.at(-1)
    const offset = previous ? previous.offset + previous.length : 0
    return [...list, { category: c.category, length: (c.total / total) * CIRCUMFERENCE, offset }]
  }, [])

  return (
    <section className="card spending" aria-labelledby="spending-heading">
      <div className="card__header">
        <h2 id="spending-heading">Spending overview</h2>
      </div>

      {slices.length === 0 ? (
        <EmptyState compact title="Nothing spent yet" message="Your spending by category appears here once you add expenses." />
      ) : (
        <div className="spending__body">
          <div className="donut">
            <svg viewBox={`0 0 ${SIZE} ${SIZE}`} aria-hidden="true">
              <circle className="donut__track" cx={SIZE / 2} cy={SIZE / 2} r={RADIUS} strokeWidth={STROKE} />
              {segments.map((s) => (
                <circle
                  key={s.category}
                  cx={SIZE / 2}
                  cy={SIZE / 2}
                  r={RADIUS}
                  fill="none"
                  stroke={CATEGORY_COLORS[s.category]}
                  strokeWidth={STROKE}
                  strokeDasharray={`${Math.max(s.length - (segments.length > 1 ? GAP : 0), 0.5)} ${CIRCUMFERENCE}`}
                  strokeDashoffset={-s.offset}
                  transform={`rotate(-90 ${SIZE / 2} ${SIZE / 2})`}
                />
              ))}
            </svg>
            <div className="donut__center">
              <span className="donut__total">{formatPeso(total)}</span>
              <span className="donut__caption">Total spent</span>
            </div>
          </div>

          <ul className="legend">
            {slices.map((c) => (
              <li key={c.category} className="legend__item">
                <span className="legend__dot" style={{ background: CATEGORY_COLORS[c.category] }} aria-hidden="true" />
                <span className="legend__name">{c.category}</span>
                <span className="legend__amount">{formatPeso(c.total)}</span>
                <span className="legend__share">{formatPercent((c.total / total) * 100)}</span>
              </li>
            ))}
          </ul>
        </div>
      )}
    </section>
  )
}
