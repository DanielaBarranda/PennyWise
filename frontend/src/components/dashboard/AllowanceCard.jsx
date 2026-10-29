import { useState } from 'react'
import { ErrorState, LoadingState } from '../common/States.jsx'
import { useAsync } from '../../hooks/useAsync.js'
import { useAuth } from '../../hooks/useAuth.js'
import { useToast } from '../../hooks/useToast.js'
import { api } from '../../services/api.js'
import { formatPeso } from '../../utils/format.js'

const PERIODS = [
  { value: 'Daily', label: 'Daily' },
  { value: 'Weekly', label: 'Weekly' },
  { value: 'Monthly', label: 'Monthly' },
]

const days = (n) => `${n} day${n === 1 ? '' : 's'}`

function periodText(period, a) {
  if (period === 'Daily') return `a day for the next ${days(a.daysLeft)}`
  if (period === 'Weekly') return `for this week (${days(a.daysLeftInWeek)}, through Sunday)`
  return `for the rest of the month (${days(a.daysLeft)})`
}

// How much can still be spent, split over the days left in the month. The
// monthly budget stays the limit; daily and weekly are just ways of reading it.
// The choice is saved on the account, so it is the same on every device.
export default function AllowanceCard({ month, refreshKey }) {
  const { user, updatePreferences } = useAuth()
  const toast = useToast()
  const [period, setPeriod] = useState(user.allowancePeriod ?? 'Monthly')
  const { data, error, reload } = useAsync(() => api.getAllowance(month), [month, refreshKey])
  const allowance = data?.month === month ? data : null

  async function choose(next) {
    const previous = period
    setPeriod(next)
    try {
      await updatePreferences({ allowancePeriod: next })
    } catch {
      setPeriod(previous)
      toast.error('Your allowance view could not be saved. Please try again.')
    }
  }

  return (
    <section className="card allowance" aria-labelledby="allowance-heading">
      <div className="card__header">
        <h2 id="allowance-heading">Spending allowance</h2>
        <fieldset className="segmented">
          <legend className="visually-hidden">Show allowance</legend>
          {PERIODS.map((p) => (
            <label key={p.value} className="segmented__option">
              <input type="radio" name="allowance-period" value={p.value} checked={period === p.value} onChange={() => choose(p.value)} />
              <span>{p.label}</span>
            </label>
          ))}
        </fieldset>
      </div>

      {error && !allowance ? (
        <ErrorState error={error} onRetry={reload} />
      ) : !allowance ? (
        <LoadingState label="Working out your allowance…" variant="rows" />
      ) : (
        <AllowanceBody allowance={allowance} period={period} />
      )}
    </section>
  )
}

function AllowanceBody({ allowance: a, period }) {
  if (a.status === 'NoBudget') {
    return <p className="allowance__message">Set a monthly budget to see how much you can spend each day, week or month.</p>
  }
  if (a.status === 'MonthEnded') {
    return <p className="allowance__message">This month has ended. Pick the current month to see what you can still spend.</p>
  }

  const amount = { Daily: a.daily, Weekly: a.weekly, Monthly: a.monthly }[period]

  return (
    <>
      <p className="allowance__amount" aria-live="polite">
        <span className="allowance__value">{formatPeso(amount)}</span>
        <span className="allowance__period">{periodText(period, a)}</span>
      </p>
      {a.status === 'OverBudget' && (
        <p className="allowance__warning">You are {formatPeso(-a.available)} over this month’s budget, so there is nothing left to spend.</p>
      )}
      <dl className="allowance__math" aria-label="How this is worked out">
        <div><dt>Budget</dt><dd>{formatPeso(a.budget)}</dd></div>
        <div><dt>Spent</dt><dd>− {formatPeso(a.spent)}</dd></div>
        <div><dt>Unpaid bills this month</dt><dd>− {formatPeso(a.reservedForBills)}</dd></div>
        <div className="allowance__total"><dt>Left to spend</dt><dd>{formatPeso(Math.max(a.available, 0))}</dd></div>
      </dl>
    </>
  )
}
