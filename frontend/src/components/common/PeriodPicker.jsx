import Icon from './Icon.jsx'
import { PERIOD_UNIT, periodLabel, shiftPeriod } from '../../utils/budgetPeriod.js'

// Previous/next buttons around the selected day, week or month. `start` is the
// first day of the period ("YYYY-MM-DD").
export default function PeriodPicker({ period, start, onChange }) {
  const unit = PERIOD_UNIT[period]

  return (
    <div className="month-picker" role="group" aria-label={`Selected ${unit}`}>
      <div className="month-picker__control">
        <button type="button" className="month-picker__step" onClick={() => onChange(shiftPeriod(period, start, -1))} aria-label={`Previous ${unit}`}>
          <Icon name="chevronLeft" />
        </button>
        <span className="month-picker__label" aria-live="polite">
          <Icon name="calendar" size={16} />
          {periodLabel(period, start)}
        </span>
        <button type="button" className="month-picker__step" onClick={() => onChange(shiftPeriod(period, start, 1))} aria-label={`Next ${unit}`}>
          <Icon name="chevronRight" />
        </button>
      </div>
    </div>
  )
}
