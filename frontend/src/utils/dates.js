const pad = (n) => String(n).padStart(2, '0')

// Months are passed around as "YYYY-MM" strings, the same format the API uses.
export function currentMonth() {
  const now = new Date()
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}`
}

export function todayIso() {
  const now = new Date()
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`
}

export function addMonths(month, delta) {
  const [year, monthIndex] = month.split('-').map(Number)
  const date = new Date(year, monthIndex - 1 + delta, 1)
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}`
}

// A sensible default date for a new expense in the selected month:
// today if that month is the current one, otherwise the 1st.
export function defaultDateForMonth(month) {
  return month === currentMonth() ? todayIso() : `${month}-01`
}
