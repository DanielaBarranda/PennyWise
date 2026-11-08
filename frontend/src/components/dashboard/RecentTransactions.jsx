import CategoryBadge, { CategoryIcon } from '../common/CategoryBadge.jsx'
import Icon from '../common/Icon.jsx'
import { EmptyState } from '../common/States.jsx'
import { THIS_PERIOD } from '../../utils/budgetPeriod.js'
import { formatDate, formatPeso } from '../../utils/format.js'

const RECENT_COUNT = 5

// Expenses and savings moves both use the period's budget, so the list shows
// the newest five of either kind.
function mergeRecent(expenses, savings) {
  const rows = [
    ...expenses.map((e) => ({
      key: `e${e.id}`, date: e.expenseDate, createdAt: e.createdAt, title: e.description,
      category: e.category, icon: undefined, amount: formatPeso(e.amount), href: '#/expenses',
    })),
    ...savings.map((s) => {
      const deposit = s.type === 'Contribution'
      return {
        key: `s${s.id}`, date: s.date, createdAt: s.createdAt,
        title: deposit ? `Saved for ${s.goalName}` : `Took out of ${s.goalName}`,
        category: 'Savings', icon: 'piggy',
        // A withdrawal gives money back to the budget.
        amount: deposit ? formatPeso(s.amount) : `+${formatPeso(s.amount)}`,
      }
    }),
  ]
  rows.sort((a, b) => (a.date === b.date ? String(b.createdAt).localeCompare(String(a.createdAt)) : b.date.localeCompare(a.date)))
  return rows.slice(0, RECENT_COUNT)
}

export default function RecentTransactions({ expenses, savings = [], period, onAdd }) {
  const rows = mergeRecent(expenses, savings)
  return (
    <section className="card card--flush" aria-labelledby="recent-heading">
      <div className="card__header card__header--padded">
        <h2 id="recent-heading">Recent transactions</h2>
        {rows.length > 0 && (
          <a href="#/expenses" className="button button--link">
            View all
          </a>
        )}
      </div>

      {rows.length === 0 ? (
        <div className="card__body">
          <EmptyState
            compact
            title={`No expenses ${THIS_PERIOD[period] ?? 'this month'}`}
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
            <caption className="visually-hidden">Five most recent expenses and savings {THIS_PERIOD[period] ?? 'this month'}</caption>
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
              {rows.map((r) => (
                <tr key={r.key}>
                  <td className="data-table__muted data-table__date">{formatDate(r.date)}</td>
                  <td>
                    <div className="data-table__cell">
                      <CategoryIcon category={r.category} icon={r.icon} size="sm" shape="round" />
                      <span className="data-table__text">
                        <span className="data-table__strong">{r.title}</span>
                        {/* Phones hide the Date and Category columns; this line replaces them. */}
                        <span className="data-table__mobile-meta">
                          {r.category}, {formatDate(r.date)}
                        </span>
                      </span>
                    </div>
                  </td>
                  <td className="data-table__category">
                    <CategoryBadge category={r.category} icon={r.icon} />
                  </td>
                  <td className="num data-table__strong data-table__amount">{r.amount}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}
