import { CategoryIcon } from '../common/CategoryBadge.jsx'
import Icon from '../common/Icon.jsx'
import { formatDate, formatPeso } from '../../utils/format.js'

// A real <table> for screen readers; on small screens CSS turns each row into
// a compact card.
export default function ExpenseTable({ expenses, caption, onEdit, onDelete }) {
  return (
    <div className="table-wrap">
      <table className="expense-table">
        <caption className="visually-hidden">{caption}</caption>
        <thead>
          <tr>
            <th scope="col">Expense</th>
            <th scope="col">Date</th>
            <th scope="col" className="num">
              Amount
            </th>
            <th scope="col">
              <span className="visually-hidden">Actions</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {expenses.map((expense) => (
            <tr key={expense.id}>
              <td className="expense-table__expense">
                {/* The flex layout lives on an inner element: a flex <td> stops behaving like a table cell. */}
                <div className="expense-table__cell">
                  <CategoryIcon category={expense.category} />
                  <span className="expense-table__text">
                    <span className="expense-table__description">{expense.description}</span>
                    <span className="expense-table__category">{expense.category}</span>
                    {/* Shown only on phones, where the Date column is hidden. */}
                    <span className="expense-table__mobile-date">{formatDate(expense.expenseDate)}</span>
                  </span>
                </div>
              </td>
              <td className="expense-table__date">{formatDate(expense.expenseDate)}</td>
              <td className="num expense-table__amount">{formatPeso(expense.amount)}</td>
              <td className="expense-table__actions">
                <button
                  type="button"
                  className="icon-button"
                  onClick={() => onEdit(expense)}
                  aria-label={`Edit ${expense.description}`}
                  title="Edit"
                >
                  <Icon name="pencil" />
                </button>
                <button
                  type="button"
                  className="icon-button icon-button--danger"
                  onClick={() => onDelete(expense)}
                  aria-label={`Delete ${expense.description}`}
                  title="Delete"
                >
                  <Icon name="trash" />
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
