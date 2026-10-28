import Icon from './Icon.jsx'
import { CATEGORY_COLORS } from '../../utils/constants.js'

// The category's icon on a tint of its color. Used wherever a category appears,
// so people learn to recognise categories by shape as well as by name.
export function CategoryIcon({ category, size = 'md', shape = 'square' }) {
  return (
    <span
      className={`category-icon category-icon--${size}${shape === 'round' ? ' category-icon--round' : ''}`}
      style={{ '--cat': CATEGORY_COLORS[category] }}
    >
      <Icon name={category} size={size === 'sm' ? 14 : 18} />
    </span>
  )
}

export default function CategoryBadge({ category }) {
  return (
    <span className="category-badge">
      <CategoryIcon category={category} size="sm" shape="round" />
      {category}
    </span>
  )
}
