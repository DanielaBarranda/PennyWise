import { CATEGORY_COLORS } from '../../utils/constants.js'

export default function CategoryBadge({ category }) {
  return (
    <span className="category-badge">
      <span className="category-badge__dot" style={{ background: CATEGORY_COLORS[category] }} aria-hidden="true" />
      {category}
    </span>
  )
}
