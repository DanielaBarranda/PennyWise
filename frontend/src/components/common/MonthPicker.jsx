import { addMonths, currentMonth } from '../../utils/dates.js'
import { formatMonth } from '../../utils/format.js'

// Previous/next buttons work in every browser, unlike <input type="month">,
// which Safari and Firefox on desktop show as a plain text box.
export default function MonthPicker({ month, onChange }) {
  const isCurrent = month === currentMonth()

  return (
    <div className="month-picker" role="group" aria-label="Selected month">
      <button type="button" className="icon-button" onClick={() => onChange(addMonths(month, -1))} aria-label="Previous month">
        ‹
      </button>
      <span className="month-picker__label" aria-live="polite">
        {formatMonth(month)}
      </span>
      <button type="button" className="icon-button" onClick={() => onChange(addMonths(month, 1))} aria-label="Next month">
        ›
      </button>
      {!isCurrent && (
        <button type="button" className="button button--link" onClick={() => onChange(currentMonth())}>
          This month
        </button>
      )}
    </div>
  )
}
