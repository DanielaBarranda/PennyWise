import { CategoryIcon } from '../common/CategoryBadge.jsx'
import { CATEGORIES } from '../../utils/constants.js'

// Toggle buttons instead of a dropdown: every category is visible and one click away.
// aria-pressed tells screen readers which filter is active.
export default function CategoryFilter({ value, onChange }) {
  return (
    <div className="chip-row" role="group" aria-label="Filter by category">
      <button type="button" className="chip" aria-pressed={value === ''} onClick={() => onChange('')}>
        All categories
      </button>
      {CATEGORIES.map((c) => (
        <button key={c} type="button" className="chip" aria-pressed={value === c} onClick={() => onChange(c)}>
          <CategoryIcon category={c} size="sm" />
          {c}
        </button>
      ))}
    </div>
  )
}
