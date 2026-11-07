import { useState } from 'react'
import { ErrorState, LoadingState } from '../common/States.jsx'
import { useAsync } from '../../hooks/useAsync.js'
import { api } from '../../services/api.js'
import { PERIOD_UNIT, periodQuery } from '../../utils/budgetPeriod.js'
import { formatPeso } from '../../utils/format.js'

const days = (n) => `${n} day${n === 1 ? '' : 's'}`

// What is left to spend in the selected period, from that period's own budget.
// "Per day" divides what is left by the days left; it never adds money.
export default function AllowanceCard({ period, start, refreshKey }) {
  const [view, setView] = useState('total')
  const { data, error, reload } = useAsync(() => api.getAllowance(periodQuery(period, start)), [period, start, refreshKey])
  const allowance = data?.period === period && data?.start === start ? data : null
  const unit = PERIOD_UNIT[period]
  // A single day has nothing to divide, and without a budget there is nothing to show.
  const showViews = period !== 'Daily' && (allowance?.status === 'Available' || allowance?.status === 'OverBudget')

  return (
    <section className="card allowance" aria-labelledby="allowance-heading" data-tour="left-to-spend">
      <div className="card__header">
        <h2 id="allowance-heading">Left to spend</h2>
        {showViews && (
          <fieldset className="segmented">
            <legend className="visually-hidden">Show</legend>
            {[
              ['total', `This ${unit}`],
              ['daily', 'Per day'],
            ].map(([value, label]) => (
              <label key={value} className="segmented__option">
                <input type="radio" name="allowance-view" value={value} checked={view === value} onChange={() => setView(value)} />
                <span>{label}</span>
              </label>
            ))}
          </fieldset>
        )}
      </div>

      {error && !allowance ? (
        <ErrorState error={error} onRetry={reload} />
      ) : !allowance ? (
        <LoadingState label="Working out what is left…" variant="rows" />
      ) : (
        <AllowanceBody allowance={allowance} unit={unit} daily={showViews && view === 'daily'} />
      )}
    </section>
  )
}

function AllowanceBody({ allowance: a, unit, daily }) {
  if (a.status === 'NoBudget') {
    return <p className="allowance__message">Set a budget for this {unit} to see how much you can still spend.</p>
  }
  if (a.status === 'Ended') {
    return <p className="allowance__message">This {unit} has ended. Go to the current {unit} to see what you can still spend.</p>
  }

  return (
    <>
      <p className="allowance__amount" aria-live="polite">
        <span className="allowance__value">{formatPeso(daily ? a.daily : a.total)}</span>
        <span className="allowance__period">
          {daily ? `a day for the next ${days(a.daysLeft)}` : unit === 'day' ? 'left today' : `left this ${unit} (${days(a.daysLeft)} to go)`}
        </span>
      </p>
      {a.status === 'OverBudget' && (
        <p className="allowance__warning">You are {formatPeso(-a.available)} over this {unit}’s budget, so there is nothing left to spend.</p>
      )}
      <dl className="allowance__math" aria-label="How this is worked out">
        <div><dt>Budget</dt><dd>{formatPeso(a.budget)}</dd></div>
        <div><dt>Spent</dt><dd>− {formatPeso(a.spent)}</dd></div>
        {a.saved > 0 && <div><dt>Moved to savings this {unit}</dt><dd>− {formatPeso(a.saved)}</dd></div>}
        <div><dt>Unpaid bills due this {unit}</dt><dd>− {formatPeso(a.reservedForBills)}</dd></div>
        <div className="allowance__total"><dt>Left to spend</dt><dd>{formatPeso(Math.max(a.available, 0))}</dd></div>
      </dl>
    </>
  )
}
