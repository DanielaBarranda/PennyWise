// Budget periods: a day, a week (Monday to Sunday) or a calendar month. Each
// period has its own budget; nothing is ever converted between periods.
// Dates are "YYYY-MM-DD" strings, built from parts so time zones never shift them.

export const PERIODS = ['Daily', 'Weekly', 'Monthly']
export const PERIOD_UNIT = { Daily: 'day', Weekly: 'week', Monthly: 'month' }
export const THIS_PERIOD = { Daily: 'today', Weekly: 'this week', Monthly: 'this month' }

const pad = (n) => String(n).padStart(2, '0')
const toIso = (d) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
const fromIso = (iso) => {
  const [y, m, d] = iso.slice(0, 10).split('-').map(Number)
  return new Date(y, m - 1, d)
}

// The first day of the period containing `iso`: the day, its Monday, or the 1st.
export function periodStart(period, iso) {
  const d = fromIso(iso)
  if (period === 'Weekly') d.setDate(d.getDate() - ((d.getDay() + 6) % 7))
  if (period === 'Monthly') d.setDate(1)
  return toIso(d)
}

export function periodEnd(period, start) {
  const d = fromIso(start)
  if (period === 'Weekly') d.setDate(d.getDate() + 6)
  if (period === 'Monthly') d.setMonth(d.getMonth() + 1, 0)
  return toIso(d)
}

// One period earlier (-1) or later (+1).
export function shiftPeriod(period, start, delta) {
  const d = fromIso(start)
  if (period === 'Daily') d.setDate(d.getDate() + delta)
  if (period === 'Weekly') d.setDate(d.getDate() + 7 * delta)
  if (period === 'Monthly') d.setMonth(d.getMonth() + delta, 1)
  return toIso(d)
}

const fmt = (iso, options) => fromIso(iso).toLocaleDateString('en-PH', options)

// "November 2026", "Nov 2 – 8, 2026", "Oct 26 – Nov 1, 2026" or "Mon, Nov 2, 2026".
export function periodLabel(period, start) {
  if (period === 'Monthly') return fmt(start, { month: 'long', year: 'numeric' })
  if (period === 'Daily') return fmt(start, { weekday: 'short', month: 'short', day: 'numeric', year: 'numeric' })
  const end = periodEnd(period, start)
  const sameYear = start.slice(0, 4) === end.slice(0, 4)
  const sameMonth = sameYear && start.slice(5, 7) === end.slice(5, 7)
  const from = fmt(start, sameYear ? { month: 'short', day: 'numeric' } : { month: 'short', day: 'numeric', year: 'numeric' })
  // Built by hand: Intl formats "day + year" without a month oddly ("2026 (day: 8)").
  const to = sameMonth ? `${Number(end.slice(8, 10))}, ${end.slice(0, 4)}` : fmt(end, { month: 'short', day: 'numeric', year: 'numeric' })
  return `${from} – ${to}`
}

// The API query for a period: ?month= for a month, ?period=&date= otherwise.
export function periodQuery(period, start) {
  return period === 'Monthly' ? { month: start.slice(0, 7) } : { period, date: start }
}

// "October 2026 budget", "budget for Nov 2 – 8, 2026" or "budget for Mon, Nov 2, 2026".
export function budgetName(period, start) {
  return period === 'Monthly' ? `${periodLabel(period, start)} budget` : `budget for ${periodLabel(period, start)}`
}
