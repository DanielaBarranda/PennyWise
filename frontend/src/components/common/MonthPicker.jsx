import Icon from './Icon.jsx'
import { addMonths, currentMonth } from '../../utils/dates.js'
import { formatMonth } from '../../utils/format.js'

// Previous/next buttons work in every browser, unlike <input type="month">,
// which Safari and Firefox on desktop show as a plain text box.
export default function MonthPicker({ month, onChange }) {
  const isCurrent = month === currentMonth()

  return (
    <div className="month-picker" role="group" aria-label="Selected month">
      <div className="month-picker__control">
        <button type="button" className="month-picker__step" onClick={() => onChange(addMonths(month, -1))} aria-label="Previous month">
          <Icon name="chevronLeft" />
        </button>
        <span className="month-picker__label" aria-live="polite">
          <Icon name="calendar" size={16} />
          {formatMonth(month)}
        </span>
        <button type="button" className="month-picker__step" onClick={() => onChange(addMonths(month, 1))} aria-label="Next month">
          <Icon name="chevronRight" />
        </button>
      </div>
      {!isCurrent && (
        <button type="button" className="button button--link" onClick={() => onChange(currentMonth())}>
          Back to this month
        </button>
      )}
    </div>
  )
}
