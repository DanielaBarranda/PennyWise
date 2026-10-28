import CategoryBadge from '../common/CategoryBadge.jsx'
import { formatDate, formatPeso } from '../../utils/format.js'

// A real <table> for screen readers; on small screens CSS turns each row into
// a card, using data-label to show the column name beside each value.
export default function ExpenseTable({ expenses, caption, onEdit, onDelete }) {
  return (
    <div className="table-wrap">
      <table className="expense-table">
        <caption className="visually-hidden">{caption}</caption>
        <thead>
          <tr>
            <th scope="col">Date</th>
            <th scope="col">Description</th>
            <th scope="col">Category</th>
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
              <td data-label="Date">{formatDate(expense.expenseDate)}</td>
              <td data-label="Description" className="expense-table__description">
                {expense.description}
              </td>
              <td data-label="Category">
                <CategoryBadge category={expense.category} />
              </td>
              <td data-label="Amount" className="num amount">
                {formatPeso(expense.amount)}
              </td>
              <td className="expense-table__actions">
                <button
                  type="button"
                  className="button button--small button--ghost"
                  onClick={() => onEdit(expense)}
                  aria-label={`Edit ${expense.description}`}
                >
                  Edit
                </button>
                <button
                  type="button"
                  className="button button--small button--danger-ghost"
                  onClick={() => onDelete(expense)}
                  aria-label={`Delete ${expense.description}`}
                >
                  Delete
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
