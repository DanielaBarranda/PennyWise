const pesoFormatter = new Intl.NumberFormat('en-PH', {
  style: 'currency',
  currency: 'PHP',
  minimumFractionDigits: 2,
})

export function formatPeso(value) {
  return pesoFormatter.format(Number(value ?? 0))
}

export function formatPercent(value) {
  return `${Math.round(Number(value ?? 0))}%`
}

// "2026-10" -> "October 2026"
export function formatMonth(month) {
  const [year, monthIndex] = month.split('-').map(Number)
  return new Date(year, monthIndex - 1, 1).toLocaleDateString('en-PH', { month: 'long', year: 'numeric' })
}

// "2026-10-05" -> "Oct 5, 2026". Built from parts so the browser's time zone
// can never shift the date by one day.
export function formatDate(isoDate) {
  const [year, month, day] = isoDate.slice(0, 10).split('-').map(Number)
  return new Date(year, month - 1, day).toLocaleDateString('en-PH', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  })
}
