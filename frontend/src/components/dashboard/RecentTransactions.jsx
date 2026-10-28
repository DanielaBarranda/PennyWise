import CategoryBadge from '../common/CategoryBadge.jsx'
import { EmptyState } from '../common/States.jsx'
import { formatDate, formatPeso } from '../../utils/format.js'

export default function RecentTransactions({ expenses, onAdd }) {
  return (
    <section className="card" aria-labelledby="recent-heading">
      <div className="card__header">
        <h2 id="recent-heading">Recent transactions</h2>
        {expenses.length > 0 && (
          <a href="#/expenses" className="button button--link">
            View all
          </a>
        )}
      </div>

      {expenses.length === 0 ? (
        <EmptyState
          title="No transactions this month"
          message="Record your first expense to start tracking."
          action={
            <button type="button" className="button button--secondary" onClick={onAdd}>
              Add expense
            </button>
          }
        />
      ) : (
        <ul className="recent">
          {expenses.map((e) => (
            <li key={e.id} className="recent__item">
              <div className="recent__main">
                <span className="recent__description">{e.description}</span>
                <span className="recent__meta">
                  <CategoryBadge category={e.category} /> · {formatDate(e.expenseDate)}
                </span>
              </div>
              <span className="recent__amount amount">{formatPeso(e.amount)}</span>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
