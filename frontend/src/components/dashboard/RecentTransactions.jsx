import CategoryBadge, { CategoryIcon } from '../common/CategoryBadge.jsx'
import Icon from '../common/Icon.jsx'
import { EmptyState } from '../common/States.jsx'
import { formatDate, formatPeso } from '../../utils/format.js'

export default function RecentTransactions({ expenses, onAdd }) {
  return (
    <section className="card card--flush" aria-labelledby="recent-heading">
      <div className="card__header card__header--padded">
        <h2 id="recent-heading">Recent transactions</h2>
        {expenses.length > 0 && (
          <a href="#/expenses" className="button button--link">
            View all
          </a>
        )}
      </div>

      {expenses.length === 0 ? (
        <div className="card__body">
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
        </div>
      ) : (
        <div className="table-wrap">
          <table className="data-table">
            <caption className="visually-hidden">Five most recent expenses this month</caption>
            <thead>
              <tr>
                <th scope="col">Date</th>
                <th scope="col">Description</th>
                <th scope="col">Category</th>
                <th scope="col" className="num">
                  Amount
                </th>
              </tr>
            </thead>
            <tbody>
              {expenses.map((e) => (
                <tr key={e.id}>
                  <td className="data-table__muted data-table__date">{formatDate(e.expenseDate)}</td>
                  <td>
                    <div className="data-table__cell">
                      <CategoryIcon category={e.category} size="sm" shape="round" />
                      <span className="data-table__text">
                        <span className="data-table__strong">{e.description}</span>
                        {/* Phones hide the Date and Category columns; this line replaces them. */}
                        <span className="data-table__mobile-meta">
                          {e.category}, {formatDate(e.expenseDate)}
                        </span>
                      </span>
                    </div>
                  </td>
                  <td className="data-table__category">
                    <CategoryBadge category={e.category} />
                  </td>
                  <td className="num data-table__strong data-table__amount">{formatPeso(e.amount)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}
