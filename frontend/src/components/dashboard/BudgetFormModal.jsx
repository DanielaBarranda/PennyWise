import { useRef, useState } from 'react'
import Modal from '../common/Modal.jsx'
import FormField from '../common/FormField.jsx'
import Icon from '../common/Icon.jsx'
import { CategoryIcon } from '../common/CategoryBadge.jsx'
import { api } from '../../services/api.js'
import { useAuth } from '../../hooks/useAuth.js'
import { useToast } from '../../hooks/useToast.js'
import {
  PERIODS,
  PERIOD_BUDGET_LABELS,
  PERIOD_HINTS,
  PERIOD_WORDS,
  amountText,
  daysInMonth,
  fromMonthly,
  toMonthly,
} from '../../utils/budgetPeriod.js'
import { CATEGORIES, MAX_AMOUNT } from '../../utils/constants.js'
import { formatMonth, formatPeso } from '../../utils/format.js'
import { validateAmount, validateBudget } from '../../utils/validation.js'

// `budget` is null when the month has no budget yet (create), otherwise it is edited.
export default function BudgetFormModal({ open, month, budget, onClose, onSaved }) {
  const [saving, setSaving] = useState(false)

  return (
    <Modal
      open={open}
      wide
      title={`${budget ? 'Edit' : 'Set'} budget for ${formatMonth(month)}`}
      description="Your spending limit, plus optional limits for individual categories."
      onClose={saving ? () => {} : onClose}
    >
      <BudgetForm month={month} budget={budget} onCancel={onClose} onSaved={onSaved} onSavingChange={setSaving} />
    </Modal>
  )
}

// Amounts are typed in the chosen period (day, week or month) and saved as
// monthly amounts. The choice is the account's period preference, which the
// dashboard's budget and allowance cards also follow.
function BudgetForm({ month, budget, onCancel, onSaved, onSavingChange }) {
  const toast = useToast()
  const { user, updatePreferences } = useAuth()
  const formRef = useRef(null)
  const [period, setPeriod] = useState(user.allowancePeriod ?? 'Monthly')
  const [form, setForm] = useState(() => {
    const show = (monthly) => amountText(fromMonthly(monthly, period, month))
    return {
      amount: budget ? show(budget.amount) : '',
      categoryLimits: Object.fromEntries(
        CATEGORIES.map((c) => {
          const limit = budget?.categoryLimits?.find((l) => l.category === c)
          return [c, limit ? show(limit.amount) : '']
        }),
      ),
    }
  })
  const [errors, setErrors] = useState({})
  const [formError, setFormError] = useState(null)
  const [saving, setSaving] = useState(false)

  function showErrors(fieldErrors) {
    setErrors(fieldErrors)
    requestAnimationFrame(() => formRef.current?.querySelector('[aria-invalid="true"]')?.focus())
  }

  // Switching period keeps the same money: ₱150 a day becomes ₱1,050 a week.
  function changePeriod(next) {
    const convert = (raw) =>
      raw.trim() !== '' && validateAmount(raw, { required: false }) === null
        ? amountText(fromMonthly(toMonthly(raw, period, month), next, month))
        : raw
    setForm((f) => ({
      amount: convert(f.amount),
      categoryLimits: Object.fromEntries(CATEGORIES.map((c) => [c, convert(f.categoryLimits[c])])),
    }))
    setErrors({})
    setFormError(null)
    setPeriod(next)
  }

  function setBusy(value) {
    setSaving(value)
    onSavingChange(value)
  }

  async function handleSubmit(event) {
    event.preventDefault()
    setFormError(null)
    const clientErrors = validateBudget(form, PERIOD_BUDGET_LABELS[period])
    const monthlyAmount = clientErrors.amount ? 0 : toMonthly(form.amount, period, month)
    if (monthlyAmount > MAX_AMOUNT) {
      clientErrors.amount = `That comes to ${formatPeso(monthlyAmount)} for the month, more than the ₱${MAX_AMOUNT.toLocaleString('en-PH')} maximum.`
    }
    if (Object.keys(clientErrors).length > 0) {
      if (clientErrors.categoryLimits) setFormError(clientErrors.categoryLimits)
      return showErrors(clientErrors)
    }

    const payload = {
      month,
      amount: monthlyAmount,
      categoryLimits: CATEGORIES.filter((c) => form.categoryLimits[c].trim() !== '').map((c) => ({
        category: c,
        amount: toMonthly(form.categoryLimits[c], period, month, { roundDown: true }),
      })),
    }

    setBusy(true)
    try {
      const saved = budget ? await api.updateBudget(budget.id, payload) : await api.createBudget(payload)
      toast.success(`Budget for ${formatMonth(month)} saved.`)
      if (period !== user.allowancePeriod) {
        // The budget is already saved; only the display preference failed.
        updatePreferences({ allowancePeriod: period }).catch(() =>
          toast.error('Your budget was saved, but the daily/weekly/monthly choice could not be saved.'),
        )
      }
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
          <Icon name="alert" size={18} />
          {formError}
        </p>
      )}

      <fieldset className="segmented segmented--block">
        <legend className="visually-hidden">Budget period</legend>
        {PERIODS.map((p) => (
          <label key={p} className="segmented__option">
            <input type="radio" name="budget-period" value={p} checked={period === p} onChange={() => changePeriod(p)} />
            <span>{p}</span>
          </label>
        ))}
      </fieldset>

      <FormField label={PERIOD_BUDGET_LABELS[period]} error={errors.amount} hint={PERIOD_HINTS[period]}>
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
      <MonthlyTotal amount={form.amount} period={period} month={month} />

      <fieldset className="form__fieldset">
        <legend>Category limits {period !== 'Monthly' && <span className="budget-period__unit">({PERIOD_WORDS[period]})</span>}</legend>
        <p className="field__hint">Optional. PennyWise warns you when a category gets close to its limit. Leave blank for no limit.</p>
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

// "₱150.00 × 31 days = ₱4,650.00 for October 2026", so the saved monthly amount is never a surprise.
function MonthlyTotal({ amount, period, month }) {
  if (period === 'Monthly' || validateAmount(amount) !== null) return null
  const days = daysInMonth(month)
  const times = period === 'Daily' ? `${days} days` : `${days} days ÷ 7`
  return (
    <p className="budget-period__total" aria-live="polite">
      {formatPeso(Number(amount))} × {times} = <strong>{formatPeso(toMonthly(amount, period, month))}</strong> for {formatMonth(month)}
    </p>
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
