import { useEffect, useState } from 'react'
import FormField from '../common/FormField.jsx'
import Icon from '../common/Icon.jsx'
import Modal from '../common/Modal.jsx'
import { api } from '../../services/api.js'
import { useToast } from '../../hooks/useToast.js'
import { PERIOD_UNIT, periodLabel } from '../../utils/budgetPeriod.js'
import { formatDate, formatPeso } from '../../utils/format.js'

// Shown once the period before this one is over and money was left in it.
// The user decides once: add it to this budget, move it to savings, or leave it.
// The amount is worked out by the API (GET /api/dashboard/summary leftoverOffer).
export default function LeftoverCard({ offer, budget, onDone }) {
  const [open, setOpen] = useState(false)
  const unit = PERIOD_UNIT[offer.period]
  const from = periodLabel(offer.period, offer.start)

  return (
    <>
      <div className="leftover" role="status">
        <span className="leftover__icon" aria-hidden="true">
          <Icon name="piggy" size={20} />
        </span>
        <p className="leftover__text">
          <strong>You have {formatPeso(offer.amount)} left from last {unit}</strong>
          <span>{from}. Add it to this {unit}’s budget, save it, or leave it.</span>
        </p>
        <button type="button" className="button button--primary button--small" onClick={() => setOpen(true)}>
          Choose what to do
        </button>
      </div>
      <Modal
        open={open}
        title={`${formatPeso(offer.amount)} left from last ${unit}`}
        description={`From your budget for ${from}. Where should it go?`}
        onClose={() => setOpen(false)}
      >
        {open && (
          <LeftoverForm
            offer={offer}
            budget={budget}
            unit={unit}
            from={from}
            onClose={() => setOpen(false)}
            onDone={() => {
              setOpen(false)
              onDone()
            }}
          />
        )}
      </Modal>
    </>
  )
}

function LeftoverForm({ offer, budget, unit, from, onClose, onDone }) {
  const toast = useToast()
  const [action, setAction] = useState('AddToBudget')
  const [category, setCategory] = useState('')
  const [goalId, setGoalId] = useState('')
  const [goals, setGoals] = useState(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState(null)
  const limited = budget.categoryLimits ?? []
  const amount = formatPeso(offer.amount)

  useEffect(() => {
    let live = true
    api.getSavingsGoals().then(
      (list) => {
        if (!live) return
        setGoals(list)
        if (list.length > 0) setGoalId(String(list[0].id))
      },
      () => live && setGoals([]),
    )
    return () => {
      live = false
    }
  }, [])

  const noGoals = goals !== null && goals.length === 0
  const canSubmit = !busy && !(action === 'MoveToSavings' && (!goalId || noGoals))

  async function handleSubmit(event) {
    event.preventDefault()
    if (!canSubmit) return
    setBusy(true)
    setError(null)
    try {
      const body = { action }
      if (action === 'AddToBudget' && category) body.category = category
      if (action === 'MoveToSavings') body.goalId = Number(goalId)
      const result = await api.carryOver(budget.id, body)
      if (result.action === 'AddToBudget') {
        toast.success(`Added ${amount} to this ${unit}’s budget${result.category ? ` and the ${result.category} limit` : ''}.`)
      } else if (result.action === 'MoveToSavings') {
        toast.success(`Moved ${amount} to ${result.goalName}.`)
      } else {
        toast.success(`Left ${amount} in ${from}.`)
      }
      onDone()
    } catch (err) {
      setError(err.message)
      setBusy(false)
    }
  }

  return (
    <form className="form" onSubmit={handleSubmit} noValidate>
      {error && (
        <p className="form__error" role="alert">
          <Icon name="alert" size={18} />
          {error}
        </p>
      )}
      <fieldset className="cover__options">
        <legend className="visually-hidden">What to do with {amount}</legend>

        <label className="cover__option">
          <input type="radio" name="leftover" value="AddToBudget" checked={action === 'AddToBudget'} onChange={() => setAction('AddToBudget')} />
          <span className="cover__option-name">Add to this {unit}’s budget</span>
          <span className="cover__option-room">{formatPeso(budget.amount)} → {formatPeso(budget.amount + offer.amount)}</span>
        </label>
        {action === 'AddToBudget' && limited.length > 0 && (
          <FormField label="Also raise a category limit (optional)" className="leftover__sub">
            {(props) => (
              <select {...props} value={category} onChange={(e) => setCategory(e.target.value)}>
                <option value="">No, just the budget</option>
                {limited.map((l) => (
                  <option key={l.category} value={l.category}>
                    {l.category}: {formatPeso(l.amount)} → {formatPeso(l.amount + offer.amount)}
                  </option>
                ))}
              </select>
            )}
          </FormField>
        )}

        <label className="cover__option">
          <input type="radio" name="leftover" value="MoveToSavings" checked={action === 'MoveToSavings'} onChange={() => setAction('MoveToSavings')} />
          <span className="cover__option-name">Put it in a savings goal</span>
          <span className="cover__option-room">Saved on {formatDate(offer.end)}, the last day of last {unit}</span>
        </label>
        {action === 'MoveToSavings' &&
          (noGoals ? (
            <p className="leftover__sub cover__empty">
              You have no savings goals yet. <a href="#/savings">Create one on the Savings page</a>.
            </p>
          ) : (
            <FormField label="Which goal?" className="leftover__sub">
              {(props) => (
                <select {...props} value={goalId} onChange={(e) => setGoalId(e.target.value)} disabled={goals === null}>
                  {goals === null && <option value="">Loading goals…</option>}
                  {goals?.map((g) => (
                    <option key={g.id} value={g.id}>
                      {g.name}: {formatPeso(g.savedAmount)} of {formatPeso(g.targetAmount)}
                    </option>
                  ))}
                </select>
              )}
            </FormField>
          ))}

        <label className="cover__option">
          <input type="radio" name="leftover" value="Keep" checked={action === 'Keep'} onChange={() => setAction('Keep')} />
          <span className="cover__option-name">Leave it</span>
          <span className="cover__option-room">Stays as money left in {from}</span>
        </label>
      </fieldset>
      <p className="field__hint">You decide once. The money is counted in one place only, so nothing is counted twice.</p>

      <div className="modal__actions">
        <button type="button" className="button button--ghost" onClick={onClose} disabled={busy}>
          Not now
        </button>
        <button type="submit" className="button button--primary" disabled={!canSubmit} data-autofocus>
          {busy ? 'Saving…' : action === 'AddToBudget' ? `Add ${amount}` : action === 'MoveToSavings' ? `Save ${amount}` : 'Leave it'}
        </button>
      </div>
    </form>
  )
}
