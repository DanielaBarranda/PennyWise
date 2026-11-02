import { useState } from 'react'
import { useAuth } from '../../hooks/useAuth.js'
import { useToast } from '../../hooks/useToast.js'
import { PERIODS } from '../../utils/budgetPeriod.js'

// Daily / Weekly / Monthly: which kind of budget the dashboard and insights
// work with. Saved on the account, so it is the same on every device.
export default function PeriodSwitch() {
  const { user, updatePreferences } = useAuth()
  const toast = useToast()
  const [pending, setPending] = useState(null)
  const period = pending ?? user.budgetPeriod ?? 'Monthly'

  async function choose(next) {
    setPending(next)
    try {
      await updatePreferences({ budgetPeriod: next })
    } catch {
      toast.error('Your budget period could not be changed. Please try again.')
    } finally {
      setPending(null)
    }
  }

  return (
    <fieldset className="segmented">
      <legend className="visually-hidden">Budget period</legend>
      {PERIODS.map((p) => (
        <label key={p} className="segmented__option">
          <input type="radio" name="budget-period-switch" value={p} checked={period === p} onChange={() => choose(p)} />
          <span>{p}</span>
        </label>
      ))}
    </fieldset>
  )
}
