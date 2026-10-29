import { CATEGORIES, MAX_AMOUNT, MAX_DESCRIPTION_LENGTH } from './constants.js'

// Client-side checks give quick feedback only. The API repeats every rule,
// because anyone can send requests to it without using this page.

const AMOUNT_PATTERN = /^\d+(\.\d{1,2})?$/

export function validateAmount(raw, { required = true, label = 'Amount' } = {}) {
  const value = String(raw ?? '').trim()
  if (!value) return required ? `${label} is required.` : null
  if (!AMOUNT_PATTERN.test(value)) return `${label} must be a number with up to 2 decimal places.`
  const amount = Number(value)
  if (amount <= 0) return `${label} must be greater than ₱0.00.`
  if (amount > MAX_AMOUNT) return `${label} cannot exceed ₱${MAX_AMOUNT.toLocaleString('en-PH')}.`
  return null
}

export function validateExpense(form) {
  const errors = {}
  const amountError = validateAmount(form.amount)
  if (amountError) errors.amount = amountError
  if (!CATEGORIES.includes(form.category)) errors.category = 'Choose a category.'
  if (!/^\d{4}-\d{2}-\d{2}$/.test(form.expenseDate)) errors.expenseDate = 'Choose the date of the expense.'
  const description = form.description.trim()
  if (!description) errors.description = 'Description is required.'
  else if (description.length > MAX_DESCRIPTION_LENGTH) {
    errors.description = `Description must be ${MAX_DESCRIPTION_LENGTH} characters or fewer.`
  }
  return errors
}

export function validateBudget(form, label = 'Monthly budget') {
  const errors = {}
  const amountError = validateAmount(form.amount, { label })
  if (amountError) errors.amount = amountError

  let limitTotal = 0
  for (const category of CATEGORIES) {
    const raw = form.categoryLimits[category]
    const limitError = validateAmount(raw, { required: false, label: `${category} limit` })
    if (limitError) errors[`limit-${category}`] = limitError
    else if (raw) limitTotal += Number(raw)
  }
  if (!errors.amount && limitTotal > Number(form.amount)) {
    errors.categoryLimits = `Category limits add up to more than the ${label.toLowerCase()}.`
  }
  return errors
}
