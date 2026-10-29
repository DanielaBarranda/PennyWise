// A budget can be typed per day, week or month, but it is always saved as a
// monthly amount, so reports, limits and insights only ever see one unit.

export const PERIODS = ['Daily', 'Weekly', 'Monthly']
export const PERIOD_WORDS = { Daily: 'a day', Weekly: 'a week', Monthly: 'a month' }
export const PERIOD_BUDGET_LABELS = { Daily: 'Daily budget', Weekly: 'Weekly budget', Monthly: 'Monthly budget' }
export const PERIOD_HINTS = {
  Daily: 'The most you plan to spend each day.',
  Weekly: 'The most you plan to spend each week.',
  Monthly: 'The most you plan to spend this month.',
}

export function daysInMonth(month) {
  const [year, monthNumber] = month.split('-').map(Number)
  return new Date(year, monthNumber, 0).getDate()
}

// How many of `period` fit in the month: 31 days, or 31/7 weeks, or 1 month.
function factor(period, month) {
  if (period === 'Daily') return daysInMonth(month)
  if (period === 'Weekly') return daysInMonth(month) / 7
  return 1
}

// In centavos. Category limits round down, so limits that fit the budget in
// the typed period still fit it once both are converted.
export function toMonthlyCents(amount, period, month, { roundDown = false } = {}) {
  const exact = Math.round(Number(amount) * 100) * factor(period, month)
  // The small nudge stops values like 4428.5699999 from losing a centavo.
  return roundDown ? Math.floor(exact + 1e-6) : Math.round(exact)
}

export const toMonthly = (amount, period, month, options) => toMonthlyCents(amount, period, month, options) / 100

export function fromMonthly(monthly, period, month) {
  return Math.round((Number(monthly) * 100) / factor(period, month)) / 100
}

// For prefilling an input: "150", "1050.5", never "150.00000001".
export const amountText = (value) => String(Number(value.toFixed(2)))
