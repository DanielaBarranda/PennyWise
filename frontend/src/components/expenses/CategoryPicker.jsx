import { useId } from 'react'
import { CategoryIcon } from '../common/CategoryBadge.jsx'
import { CATEGORIES } from '../../utils/constants.js'

// Native radio buttons styled as tiles: arrow keys move between categories and
// screen readers announce "Category, radio group" as usual.
export default function CategoryPicker({ value, onChange, error }) {
  const name = useId()
  const errorId = `${name}-error`

  return (
    <fieldset className={`category-picker${error ? ' category-picker--invalid' : ''}`} aria-describedby={error ? errorId : undefined}>
      <legend className="field__label">Category</legend>
      <div className="category-picker__grid">
        {CATEGORIES.map((c) => (
          <label key={c} className="category-picker__option">
            <input type="radio" name={name} value={c} checked={value === c} onChange={() => onChange(c)} />
            <CategoryIcon category={c} size="sm" />
            <span>{c}</span>
          </label>
        ))}
      </div>
      {error && (
        <p id={errorId} className="field__error">
          {error}
        </p>
      )}
    </fieldset>
  )
}
