import { useState } from 'react'
import { ErrorState, LoadingState } from '../common/States.jsx'
import { useAsync } from '../../hooks/useAsync.js'
import { useAuth } from '../../hooks/useAuth.js'
import { useToast } from '../../hooks/useToast.js'
import { api } from '../../services/api.js'
import { formatPeso } from '../../utils/format.js'
import { PERIOD_WORDS, fromMonthly } from '../../utils/budgetPeriod.js'

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
  // Shown straight away while saving; afterwards the saved preference is the
  // source, so a change made in the budget form shows here too.
  const [pending, setPending] = useState(null)
  const period = pending ?? user.allowancePeriod ?? 'Monthly'
  const { data, error, reload } = useAsync(() => api.getAllowance(month), [month, refreshKey])
  const allowance = data?.month === month ? data : null

  async function choose(next) {
    setPending(next)
    try {
      await updatePreferences({ allowancePeriod: next })
    } catch {
      toast.error('Your allowance view could not be saved. Please try again.')
    } finally {
      setPending(null)
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
      <PlanNote allowance={a} period={period} amount={amount} />
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

// The budget may be set as e.g. ₱150 a day, while the allowance is what is
// actually left per day. Say why the two differ, so they never look contradictory.
function PlanNote({ allowance: a, period, amount }) {
  if (period === 'Monthly' || a.status !== 'Available') return null
  const plan = fromMonthly(a.budget, period, a.month)
  const gap = Math.round((amount - plan) * 100) / 100
  if (Math.abs(gap) < 0.01) return null
  return (
    <p className="allowance__plan">
      Your budget plan is {formatPeso(plan)} {PERIOD_WORDS[period]}.{' '}
      {gap > 0
        ? 'You can spend more than that because you have spent less than planned so far this month.'
        : 'This is less than that because you have spent more than planned so far this month, or have unpaid bills.'}
    </p>
  )
}
