import { useEffect, useState } from 'react'
import FormField from '../common/FormField.jsx'
import Modal from '../common/Modal.jsx'
import { useToast } from '../../hooks/useToast.js'
import { api } from '../../services/api.js'
import { todayIso } from '../../utils/dates.js'
import { formatDate, formatPeso } from '../../utils/format.js'

// Marks one due date as paid and asks where the money came from:
//   - my budget: recorded as one expense, counted in the budget for the payment date
//   - savings: taken out of a goal or fund and recorded as an expense paid from
//     savings, so it doesn't use the budget
//   - already recorded: only marks it paid (the expense exists, so it isn't counted twice)
export default function PayModal({ occurrence, onClose, onSaved }) {
  return (
    <Modal open={occurrence !== null} title={occurrence ? `Pay ${occurrence.name}` : ''}
      description={occurrence ? `${formatPeso(occurrence.amount)} due ${formatDate(occurrence.dueDate)}` : undefined} onClose={onClose}>
      {occurrence && <PayForm occurrence={occurrence} onCancel={onClose} onSaved={onSaved} />}
    </Modal>
  )
}

function PayForm({ occurrence, onCancel, onSaved }) {
  const toast = useToast()
  const [paidOn, setPaidOn] = useState(todayIso())
  const [source, setSource] = useState('budget') // budget | savings | recorded
  const [goalId, setGoalId] = useState('')
  const [goals, setGoals] = useState(null)
  const [error, setError] = useState(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    let live = true
    api.getSavingsGoals().then(
      (list) => {
        if (!live) return
        setGoals(list)
        const enough = list.find((g) => g.savedAmount >= occurrence.amount)
        if (enough) setGoalId(String(enough.id))
      },
      () => live && setGoals([]),
    )
    return () => {
      live = false
    }
  }, [occurrence.amount])

  const enoughGoals = goals?.filter((g) => g.savedAmount >= occurrence.amount) ?? []
  const chosen = goals?.find((g) => String(g.id) === goalId)

  async function handleSubmit(event) {
    event.preventDefault()
    if (source === 'savings' && !chosen) return setError('Choose which savings to pay from.')
    setBusy(true)
    setError(null)
    try {
      await api.payBill(occurrence.billId, {
        dueDate: occurrence.dueDate,
        paidOn,
        recordExpense: source !== 'recorded',
        payFromGoalId: source === 'savings' ? Number(goalId) : null,
      })
      toast.success(
        source === 'budget'
          ? `Paid ${occurrence.name} and added it to your expenses.`
          : source === 'savings'
            ? `Paid ${occurrence.name} with ${formatPeso(occurrence.amount)} from ${chosen.name}.`
            : `Marked ${occurrence.name} as paid.`,
      )
      onSaved()
    } catch (err) {
      setError(err.message)
      setBusy(false)
    }
  }

  return (
    <form className="form" onSubmit={handleSubmit} noValidate>
      {error && <p className="form__error" role="alert">{error}</p>}
      <FormField label="Paid on" hint="The expense is dated on this day">
        {(props) => <input {...props} type="date" max={todayIso()} value={paidOn} onChange={(e) => setPaidOn(e.target.value)} data-autofocus />}
      </FormField>

      <fieldset className="cover__options cover__options--stacked">
        <legend>Where is the money from?</legend>
        <label className="cover__option">
          <input type="radio" name="pay-source" value="budget" checked={source === 'budget'} onChange={() => setSource('budget')} />
          <span className="cover__option-name">My budget</span>
          <span className="cover__option-room">Added to your expenses and counted in your budget for the payment date</span>
        </label>
        <label className="cover__option">
          <input type="radio" name="pay-source" value="savings" checked={source === 'savings'} onChange={() => setSource('savings')} disabled={goals !== null && goals.length === 0} />
          <span className="cover__option-name">My savings</span>
          <span className="cover__option-room">
            {goals !== null && goals.length === 0
              ? 'You have no savings yet'
              : 'Taken out of a goal or fund. Added to your expenses, but it doesn’t use your budget'}
          </span>
        </label>
        {source === 'savings' && (
          <FormField label="Pay from" className="leftover__sub" hint={enoughGoals.length === 0 && goals ? `None of your savings has ${formatPeso(occurrence.amount)} yet.` : undefined}>
            {(props) => (
              <select {...props} value={goalId} onChange={(e) => setGoalId(e.target.value)} disabled={goals === null}>
                {goals === null && <option value="">Loading savings…</option>}
                {goals !== null && enoughGoals.length === 0 && <option value="">Not enough in any savings</option>}
                {goals?.map((g) => (
                  <option key={g.id} value={g.id} disabled={g.savedAmount < occurrence.amount}>
                    {g.name}: {formatPeso(g.savedAmount)}{g.savedAmount < occurrence.amount ? ' (not enough)' : ''}
                  </option>
                ))}
              </select>
            )}
          </FormField>
        )}
        <label className="cover__option">
          <input type="radio" name="pay-source" value="recorded" checked={source === 'recorded'} onChange={() => setSource('recorded')} />
          <span className="cover__option-name">I already added it as an expense</span>
          <span className="cover__option-room">Only marks it paid, so it isn’t counted twice</span>
        </label>
      </fieldset>

      <div className="modal__actions">
        <button type="button" className="button button--ghost" onClick={onCancel} disabled={busy}>Cancel</button>
        <button type="submit" className="button button--primary" disabled={busy || (source === 'savings' && !chosen)}>
          {busy ? 'Saving…' : 'Mark paid'}
        </button>
      </div>
    </form>
  )
}
