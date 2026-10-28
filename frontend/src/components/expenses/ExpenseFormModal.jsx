import { useRef, useState } from 'react'
import Modal from '../common/Modal.jsx'
import FormField from '../common/FormField.jsx'
import Icon from '../common/Icon.jsx'
import CategoryPicker from './CategoryPicker.jsx'
import { api } from '../../services/api.js'
import { useToast } from '../../hooks/useToast.js'
import { MAX_DESCRIPTION_LENGTH } from '../../utils/constants.js'
import { defaultDateForMonth } from '../../utils/dates.js'
import { formatMonth, formatPeso } from '../../utils/format.js'
import { validateExpense } from '../../utils/validation.js'

// `expense` is null when adding, or the expense being edited.
export default function ExpenseFormModal({ open, expense, month, onClose, onSaved }) {
  const [saving, setSaving] = useState(false)

  return (
    <Modal
      open={open}
      title={expense ? 'Edit expense' : 'Add expense'}
      description={expense ? 'Changes update your totals right away.' : 'Record something you spent.'}
      onClose={saving ? () => {} : onClose}
    >
      <ExpenseForm expense={expense} month={month} onCancel={onClose} onSaved={onSaved} onSavingChange={setSaving} />
    </Modal>
  )
}

function ExpenseForm({ expense, month, onCancel, onSaved, onSavingChange }) {
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
      warnIfOverBudget(toast, payload.expenseDate.slice(0, 7), payload.category)
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

// After a save, ask the API for that month's totals and warn if a limit is near or passed.
async function warnIfOverBudget(toast, month, category) {
  let summary
  try {
    summary = await api.getDashboardSummary(month)
  } catch {
    return // The expense is already saved; a failed check should not show an error.
  }

  const label = formatMonth(month)
  const categoryInfo = summary.categories?.find((c) => c.category === category)

  if (summary.status === 'Exceeded') {
    toast.warning(`You are over your ${label} budget by ${formatPeso(-summary.remaining)}.`)
  } else if (summary.status === 'Warning') {
    toast.warning(`You have used ${Math.round(summary.percentSpent)}% of your ${label} budget.`)
  }
  if (categoryInfo?.status === 'Exceeded') {
    toast.warning(`${category} spending is over its ${formatPeso(categoryInfo.limit)} limit for ${label}.`)
  }
}
