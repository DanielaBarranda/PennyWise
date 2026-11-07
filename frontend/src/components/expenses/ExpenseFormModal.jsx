import { useRef, useState } from 'react'
import Modal from '../common/Modal.jsx'
import FormField from '../common/FormField.jsx'
import Icon from '../common/Icon.jsx'
import CategoryPicker from './CategoryPicker.jsx'
import CoverShortfallModal from './CoverShortfallModal.jsx'
import { api } from '../../services/api.js'
import { useAuth } from '../../hooks/useAuth.js'
import { useToast } from '../../hooks/useToast.js'
import { budgetName, periodQuery, periodStart } from '../../utils/budgetPeriod.js'
import { MAX_DESCRIPTION_LENGTH } from '../../utils/constants.js'
import { defaultDateForMonth } from '../../utils/dates.js'
import { formatPeso } from '../../utils/format.js'
import { shortfallFor } from '../../utils/shortfall.js'
import { validateExpense } from '../../utils/validation.js'

// `expense` is null when adding, or the expense being edited.
// After a save that takes a category over its limit, a second dialog offers to
// cover the difference from another category (CoverShortfallModal). It lives here,
// not in the form, so it can open after the expense dialog has closed.
export default function ExpenseFormModal({ open, expense, month, onClose, onSaved }) {
  const [saving, setSaving] = useState(false)
  const [cover, setCover] = useState(null)

  return (
    <>
      <Modal
        open={open}
        title={expense ? 'Edit expense' : 'Add expense'}
        description={expense ? 'Changes update your totals right away.' : 'Record something you spent.'}
        onClose={saving ? () => {} : onClose}
      >
        <ExpenseForm expense={expense} month={month} onCancel={onClose} onSaved={onSaved} onSavingChange={setSaving} onShortfall={setCover} />
      </Modal>
      <CoverShortfallModal
        cover={cover}
        onClose={() => setCover(null)}
        onMoved={() => {
          setCover(null)
          onSaved() // the page reloads its totals
        }}
      />
    </>
  )
}

function ExpenseForm({ expense, month, onCancel, onSaved, onSavingChange, onShortfall }) {
  const { user } = useAuth()
  const toast = useToast()
  const formRef = useRef(null)
  const [form, setForm] = useState(() => ({
    amount: expense ? String(expense.amount) : '',
    category: expense?.category ?? 'Food',
    description: expense?.description ?? '',
    expenseDate: expense?.expenseDate?.slice(0, 10) ?? defaultDateForMonth(month),
  }))
  const [errors, setErrors] = useState({})
  const [formError, setFormError] = useState(null)
  const [saving, setSaving] = useState(false)

  const update = (field) => (event) => setForm((f) => ({ ...f, [field]: event.target.value }))

  function showErrors(fieldErrors) {
    setErrors(fieldErrors)
    // Wait for React to render the error state, then move focus to the first problem.
    requestAnimationFrame(() => formRef.current?.querySelector('[aria-invalid="true"]')?.focus())
  }

  function setBusy(value) {
    setSaving(value)
    onSavingChange(value)
  }

  async function handleSubmit(event) {
    event.preventDefault()
    setFormError(null)
    const clientErrors = validateExpense(form)
    if (Object.keys(clientErrors).length > 0) return showErrors(clientErrors)

    const payload = {
      amount: Number(form.amount),
      category: form.category,
      description: form.description.trim(),
      expenseDate: form.expenseDate,
    }

    setBusy(true)
    try {
      const saved = expense ? await api.updateExpense(expense.id, payload) : await api.createExpense(payload)
      toast.success(expense ? 'Expense updated.' : 'Expense added.')
      setBusy(false)
      onSaved(saved)
      warnIfOverBudget(toast, user.budgetPeriod ?? 'Monthly', payload.expenseDate, payload.category, onShortfall)
    } catch (error) {
      setBusy(false)
      setFormError(error.message)
      if (Object.keys(error.fieldErrors ?? {}).length > 0) showErrors(error.fieldErrors)
    }
  }

  return (
    <form ref={formRef} className="form" onSubmit={handleSubmit} noValidate>
      {formError && (
        <p className="form__error" role="alert">
          <Icon name="alert" size={18} />
          {formError}
        </p>
      )}

      <div className="form__row">
        <FormField label="Amount" error={errors.amount}>
          {(props) => (
            <div className="input-affix input-affix--large">
              <span className="input-affix__prefix" aria-hidden="true">
                ₱
              </span>
              <input
                {...props}
                type="text"
                inputMode="decimal"
                autoComplete="off"
                placeholder="0.00"
                data-autofocus
                value={form.amount}
                onChange={update('amount')}
              />
            </div>
          )}
        </FormField>

        <FormField label="Date" error={errors.expenseDate}>
          {(props) => <input {...props} type="date" value={form.expenseDate} onChange={update('expenseDate')} />}
        </FormField>
      </div>

      <CategoryPicker
        value={form.category}
        onChange={(category) => setForm((f) => ({ ...f, category }))}
        error={errors.category}
      />

      <FormField
        label="Description"
        error={errors.description}
        hint={`${form.description.trim().length}/${MAX_DESCRIPTION_LENGTH} characters`}
      >
        {(props) => (
          <input
            {...props}
            type="text"
            maxLength={MAX_DESCRIPTION_LENGTH}
            placeholder="e.g. Jeepney fare to campus"
            value={form.description}
            onChange={update('description')}
          />
        )}
      </FormField>

      <div className="modal__actions">
        <button type="button" className="button button--ghost" onClick={onCancel} disabled={saving}>
          Cancel
        </button>
        <button type="submit" className="button button--primary" disabled={saving}>
          {saving ? 'Saving…' : expense ? 'Save changes' : 'Add expense'}
        </button>
      </div>
    </form>
  )
}

// After a save, check the budget for the expense's own day, week or month (the
// account's budget period) and warn if it is near or passed. A category over its
// limit opens the "where should the extra come from?" dialog instead of a toast.
async function warnIfOverBudget(toast, period, date, category, onShortfall) {
  const start = periodStart(period, date)
  let summary
  try {
    summary = await api.getDashboardSummary(periodQuery(period, start))
  } catch {
    return // The expense is already saved; a failed check should not show an error.
  }
  if (!summary.budget) return

  const label = budgetName(period, start)
  const shortfall = shortfallFor(summary, category)
  if (shortfall) return onShortfall({ shortfall, budget: summary.budget, label })

  if (summary.status === 'Exceeded') {
    toast.warning(`You are over your ${label} by ${formatPeso(-summary.remaining)}.`)
  } else if (summary.status === 'Warning') {
    toast.warning(`You have used ${Math.round(summary.percentSpent)}% of your ${label}.`)
  }
}
