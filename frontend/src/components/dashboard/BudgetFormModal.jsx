import { useRef, useState } from 'react'
import Modal from '../common/Modal.jsx'
import FormField from '../common/FormField.jsx'
import { api } from '../../services/api.js'
import { useToast } from '../../hooks/useToast.js'
import { CATEGORIES } from '../../utils/constants.js'
import { formatMonth } from '../../utils/format.js'
import { validateBudget } from '../../utils/validation.js'

// `budget` is null when the month has no budget yet (create), otherwise it is edited.
export default function BudgetFormModal({ open, month, budget, onClose, onSaved }) {
  const [saving, setSaving] = useState(false)

  return (
    <Modal
      open={open}
      wide
      title={`${budget ? 'Edit' : 'Set'} budget for ${formatMonth(month)}`}
      onClose={saving ? () => {} : onClose}
    >
      <BudgetForm month={month} budget={budget} onCancel={onClose} onSaved={onSaved} onSavingChange={setSaving} />
    </Modal>
  )
}

function BudgetForm({ month, budget, onCancel, onSaved, onSavingChange }) {
  const toast = useToast()
  const formRef = useRef(null)
  const [form, setForm] = useState(() => ({
    amount: budget ? String(budget.amount) : '',
    categoryLimits: Object.fromEntries(
      CATEGORIES.map((c) => {
        const limit = budget?.categoryLimits?.find((l) => l.category === c)
        return [c, limit ? String(limit.amount) : '']
      }),
    ),
  }))
  const [errors, setErrors] = useState({})
  const [formError, setFormError] = useState(null)
  const [saving, setSaving] = useState(false)

  function showErrors(fieldErrors) {
    setErrors(fieldErrors)
    requestAnimationFrame(() => formRef.current?.querySelector('[aria-invalid="true"]')?.focus())
  }

  function setBusy(value) {
    setSaving(value)
    onSavingChange(value)
  }

  async function handleSubmit(event) {
    event.preventDefault()
    setFormError(null)
    const clientErrors = validateBudget(form)
    if (Object.keys(clientErrors).length > 0) {
      if (clientErrors.categoryLimits) setFormError(clientErrors.categoryLimits)
      return showErrors(clientErrors)
    }

    const payload = {
      month,
      amount: Number(form.amount),
      categoryLimits: CATEGORIES.filter((c) => form.categoryLimits[c].trim() !== '').map((c) => ({
        category: c,
        amount: Number(form.categoryLimits[c]),
      })),
    }

    setBusy(true)
    try {
      const saved = budget ? await api.updateBudget(budget.id, payload) : await api.createBudget(payload)
      toast.success(`Budget for ${formatMonth(month)} saved.`)
      setBusy(false)
      onSaved(saved)
    } catch (error) {
      setBusy(false)
      // Errors about the limits as a whole have no single input to sit under.
      setFormError(error.fieldErrors?.categoryLimits ?? error.message)
      if (Object.keys(error.fieldErrors ?? {}).length > 0) showErrors(error.fieldErrors)
    }
  }

  return (
    <form ref={formRef} className="form" onSubmit={handleSubmit} noValidate>
      {formError && (
        <p className="form__error" role="alert">
          {formError}
        </p>
      )}

      <FormField label="Monthly budget (₱)" error={errors.amount}>
        {(props) => (
          <input
            {...props}
            type="text"
            inputMode="decimal"
            autoComplete="off"
            placeholder="0.00"
            value={form.amount}
            onChange={(e) => setForm((f) => ({ ...f, amount: e.target.value }))}
          />
        )}
      </FormField>

      <fieldset className="form__fieldset">
        <legend>Category limits (optional)</legend>
        <p className="field__hint">PennyWise warns you when a category goes over its limit. Leave blank for no limit.</p>
        <div className="form__grid">
          {CATEGORIES.map((category) => (
            <FormField key={category} label={category} error={errors[`limit-${category}`]}>
              {(props) => (
                <input
                  {...props}
                  type="text"
                  inputMode="decimal"
                  autoComplete="off"
                  placeholder="No limit"
                  value={form.categoryLimits[category]}
                  onChange={(e) =>
                    setForm((f) => ({ ...f, categoryLimits: { ...f.categoryLimits, [category]: e.target.value } }))
                  }
                />
              )}
            </FormField>
          ))}
        </div>
      </fieldset>

      <div className="modal__actions">
        <button type="button" className="button button--ghost" onClick={onCancel} disabled={saving}>
          Cancel
        </button>
        <button type="submit" className="button button--primary" disabled={saving}>
          {saving ? 'Saving…' : 'Save budget'}
        </button>
      </div>
    </form>
  )
}
