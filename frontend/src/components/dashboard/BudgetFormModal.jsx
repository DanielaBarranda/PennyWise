import { useRef, useState } from 'react'
import Modal from '../common/Modal.jsx'
import FormField from '../common/FormField.jsx'
import Icon from '../common/Icon.jsx'
import { CategoryIcon } from '../common/CategoryBadge.jsx'
import { api } from '../../services/api.js'
import { useToast } from '../../hooks/useToast.js'
import { PERIOD_UNIT, periodLabel } from '../../utils/budgetPeriod.js'
import { CATEGORIES } from '../../utils/constants.js'
import { formatPeso } from '../../utils/format.js'
import { validateAmount, validateBudget } from '../../utils/validation.js'

const HINTS = {
  Daily: 'The most you plan to spend on this day.',
  Weekly: 'The most you plan to spend this week (Monday to Sunday).',
  Monthly: 'The most you plan to spend this month.',
}

// The budget for one period (the day, week or month shown on the dashboard).
// What you type is exactly what is saved: nothing is converted to another period.
// `budget` is null when the period has no budget yet (create), otherwise it is edited.
export default function BudgetFormModal({ open, period, start, budget, onClose, onSaved, onRemoved }) {
  const [saving, setSaving] = useState(false)

  return (
    <Modal
      open={open}
      wide
      title={`${budget ? 'Edit' : 'Set'} budget for ${periodLabel(period, start)}`}
      description="Your spending limit, plus optional limits for individual categories."
      onClose={saving ? () => {} : onClose}
    >
      <BudgetForm period={period} start={start} budget={budget} onCancel={onClose} onSaved={onSaved} onRemoved={onRemoved} onSavingChange={setSaving} />
    </Modal>
  )
}

function BudgetForm({ period, start, budget, onCancel, onSaved, onRemoved, onSavingChange }) {
  const toast = useToast()
  const formRef = useRef(null)
  const unit = PERIOD_UNIT[period]
  const label = periodLabel(period, start)
  const [form, setForm] = useState(() => ({
    amount: budget ? String(budget.amount) : '',
    categoryLimits: Object.fromEntries(
      CATEGORIES.map((c) => {
        const limit = budget?.categoryLimits?.find((l) => l.category === c)
        return [c, limit ? String(limit.amount) : '']
      }),
    ),
  }))
  const [confirmingRemove, setConfirmingRemove] = useState(false)
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
    const clientErrors = validateBudget(form, `${period} budget`)
    if (Object.keys(clientErrors).length > 0) {
      if (clientErrors.categoryLimits) setFormError(clientErrors.categoryLimits)
      return showErrors(clientErrors)
    }

    const payload = {
      period,
      start,
      amount: Number(form.amount),
      categoryLimits: CATEGORIES.filter((c) => form.categoryLimits[c].trim() !== '').map((c) => ({
        category: c,
        amount: Number(form.categoryLimits[c]),
      })),
    }

    setBusy(true)
    try {
      const saved = budget ? await api.updateBudget(budget.id, payload) : await api.createBudget(payload)
      toast.success(`Budget for ${label} saved.`)
      setBusy(false)
      onSaved(saved)
    } catch (error) {
      setBusy(false)
      // Errors about the limits as a whole have no single input to sit under.
      setFormError(error.fieldErrors?.categoryLimits ?? error.message)
      if (Object.keys(error.fieldErrors ?? {}).length > 0) showErrors(error.fieldErrors)
    }
  }

  // The period goes back to having no budget. Expenses are not touched.
  async function handleRemove() {
    setFormError(null)
    setBusy(true)
    try {
      await api.deleteBudget(budget.id)
      toast.success(`Budget for ${label} removed.`)
      setBusy(false)
      onRemoved()
    } catch (error) {
      setBusy(false)
      setConfirmingRemove(false)
      setFormError(error.message)
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

      <FormField label={`${period} budget`} error={errors.amount} hint={HINTS[period]}>
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
              onChange={(e) => setForm((f) => ({ ...f, amount: e.target.value }))}
            />
          </div>
        )}
      </FormField>

      <fieldset className="form__fieldset">
        <legend>Category limits</legend>
        <p className="field__hint">Optional, for this {unit}. PennyWise warns you when a category gets close to its limit. Leave blank for no limit.</p>
        <div className="limit-list">
          {CATEGORIES.map((category) => (
            <FormField key={category} className="limit-row" label={category} error={errors[`limit-${category}`]}>
              {(props) => (
                <div className="limit-row__control">
                  <CategoryIcon category={category} size="sm" />
                  <div className="input-affix">
                    <span className="input-affix__prefix" aria-hidden="true">
                      ₱
                    </span>
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
                  </div>
                </div>
              )}
            </FormField>
          ))}
        </div>
        <Allocation form={form} />
      </fieldset>

      {confirmingRemove ? (
        <div className="budget-remove" role="group" aria-label="Remove budget">
          <p className="budget-remove__text">
            Remove the budget for {label}? Its category limits go too. Your expenses stay as they are.
          </p>
          <div className="modal__actions">
            <button type="button" className="button button--ghost" onClick={() => setConfirmingRemove(false)} disabled={saving}>
              Keep budget
            </button>
            <button type="button" className="button button--danger" onClick={handleRemove} disabled={saving}>
              {saving ? 'Removing…' : 'Remove budget'}
            </button>
          </div>
        </div>
      ) : (
        <div className="modal__actions">
          {budget && (
            <button type="button" className="button button--ghost budget-remove__start" onClick={() => setConfirmingRemove(true)} disabled={saving}>
              <Icon name="trash" />
              Remove budget
            </button>
          )}
          <button type="button" className="button button--ghost" onClick={onCancel} disabled={saving}>
            Cancel
          </button>
          <button type="submit" className="button button--primary" disabled={saving}>
            {saving ? 'Saving…' : 'Save budget'}
          </button>
        </div>
      )}
    </form>
  )
}

// How much of the budget typed above is already split into category limits.
// It only adds up what is in this form; nothing is saved until "Save budget".
function Allocation({ form }) {
  if (validateAmount(form.amount) !== null) return null
  const budget = Number(form.amount)
  const allocated = CATEGORIES.reduce((sum, c) => {
    const raw = form.categoryLimits[c]
    return raw && validateAmount(raw, { required: false }) === null ? sum + Number(raw) : sum
  }, 0)
  const over = allocated > budget
  const percent = Math.min((allocated / budget) * 100, 100)

  return (
    <div className={`allocation${over ? ' allocation--over' : ''}`} aria-live="polite">
      <div className="allocation__track" aria-hidden="true">
        <span className="allocation__fill" style={{ width: `${percent}%` }} />
      </div>
      <p className="allocation__text">
        {over ? (
          <>
            <Icon name="alert" size={15} />
            Limits add up to {formatPeso(allocated - budget)} more than the budget.
          </>
        ) : (
          <>
            {formatPeso(allocated)} of {formatPeso(budget)} assigned to categories. {formatPeso(budget - allocated)} not assigned.
          </>
        )}
      </p>
    </div>
  )
}
