import { useState } from 'react'
import FormField from '../common/FormField.jsx'
import Modal from '../common/Modal.jsx'
import { useToast } from '../../hooks/useToast.js'
import { api } from '../../services/api.js'
import { todayIso } from '../../utils/dates.js'
import { formatDate, formatPeso } from '../../utils/format.js'

// Marks one due date as paid. By default it also records the payment as one
// expense (linked to the payment), so it shows up in spending exactly once.
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
  const [recordExpense, setRecordExpense] = useState(true)
  const [error, setError] = useState(null)
  const [busy, setBusy] = useState(false)

  async function handleSubmit(event) {
    event.preventDefault()
    setBusy(true)
    try {
      await api.payBill(occurrence.billId, { dueDate: occurrence.dueDate, paidOn, recordExpense })
      toast.success(recordExpense ? `Paid ${occurrence.name} and added it to your expenses.` : `Marked ${occurrence.name} as paid.`)
      onSaved()
    } catch (err) {
      setError(err.message)
      setBusy(false)
    }
  }

  return (
    <form className="form" onSubmit={handleSubmit} noValidate>
      {error && <p className="form__error" role="alert">{error}</p>}
      <FormField label="Paid on">
        {(props) => <input {...props} type="date" max={todayIso()} value={paidOn} onChange={(e) => setPaidOn(e.target.value)} data-autofocus />}
      </FormField>
      <label className="check">
        <input type="checkbox" checked={recordExpense} onChange={(e) => setRecordExpense(e.target.checked)} />
        <span>
          <span className="check__label">Also add it to my expenses</span>
          <span className="check__hint">
            Untick if you already recorded this payment as an expense, so it isn’t counted twice.
          </span>
        </span>
      </label>
      <div className="modal__actions">
        <button type="button" className="button button--ghost" onClick={onCancel} disabled={busy}>Cancel</button>
        <button type="submit" className="button button--primary" disabled={busy}>{busy ? 'Saving…' : 'Mark paid'}</button>
      </div>
    </form>
  )
}
