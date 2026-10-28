import { CategoryIcon } from '../common/CategoryBadge.jsx'
import Icon from '../common/Icon.jsx'
import { EmptyState } from '../common/States.jsx'
import { formatDate, formatPeso } from '../../utils/format.js'

export default function RecentTransactions({ expenses, onAdd }) {
  return (
    <section className="card" aria-labelledby="recent-heading">
      <div className="card__header">
        <h2 id="recent-heading">Recent expenses</h2>
        {expenses.length > 0 && (
          <a href="#/expenses" className="button button--link">
            See all
          </a>
        )}
      </div>

      {expenses.length === 0 ? (
        <EmptyState
          compact
          title="No expenses this month"
          message="Record what you spend and PennyWise keeps the totals for you."
          action={
            <button type="button" className="button button--primary" onClick={onAdd}>
              <Icon name="plus" />
              Add your first expense
            </button>
          }
        />
      ) : (
        <ul className="recent">
          {expenses.map((e) => (
            <li key={e.id} className="recent__item">
              <CategoryIcon category={e.category} />
              <div className="recent__main">
                <span className="recent__description">{e.description}</span>
                <span className="recent__category">{e.category}</span>
              </div>
              <div className="recent__end">
                <span className="recent__amount">{formatPeso(e.amount)}</span>
                <span className="recent__date">{formatDate(e.expenseDate)}</span>
              </div>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
