import PeriodPicker from './PeriodPicker.jsx'

// Month-only pages (expenses, reports) use the same previous/next picker.
export default function MonthPicker({ month, onChange }) {
  return <PeriodPicker period="Monthly" start={`${month}-01`} onChange={(start) => onChange(start.slice(0, 7))} />
}
