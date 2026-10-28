import { useState } from 'react'
import PageHeader from '../components/common/PageHeader.jsx'
import MonthPicker from '../components/common/MonthPicker.jsx'
import ConfirmDialog from '../components/common/ConfirmDialog.jsx'
import { EmptyState, ErrorState, LoadingState } from '../components/common/States.jsx'
import ExpenseTable from '../components/expenses/ExpenseTable.jsx'
import ExpenseFormModal from '../components/expenses/ExpenseFormModal.jsx'
import { useAsync } from '../hooks/useAsync.js'
import { useToast } from '../hooks/useToast.js'
import { api } from '../services/api.js'
import { CATEGORIES } from '../utils/constants.js'
import { formatDate, formatMonth, formatPeso } from '../utils/format.js'

export default function ExpensesPage({ month, onMonthChange }) {
  const toast = useToast()
  const [category, setCategory] = useState('')
  const { data, error, loading, reload } = useAsync(
    () => api.getExpenses({ month, category }).then((expenses) => ({ month, category, expenses })),
    [month, category],
  )

  // editing: undefined = form closed, null = adding, object = editing that expense
  const [editing, setEditing] = useState(undefined)
  const [deleting, setDeleting] = useState(null)
  const [deleteBusy, setDeleteBusy] = useState(false)

  const current = data?.month === month && data?.category === category ? data.expenses : null
  const filterLabel = `${category ? `${category} expenses` : 'Expenses'} in ${formatMonth(month)}`

  async function confirmDelete() {
    setDeleteBusy(true)
    try {
      await api.deleteExpense(deleting.id)
      toast.success(`Deleted “${deleting.description}”.`)
      setDeleting(null)
      reload()
    } catch (err) {
      toast.error(err.message)
    } finally {
      setDeleteBusy(false)
    }
  }

  return (
    <>
      <PageHeader title="Expenses" subtitle="Record, review and correct your spending">
        <button type="button" className="button button--primary" onClick={() => setEditing(null)}>
          Add expense
        </button>
      </PageHeader>

      <div className="toolbar">
        <MonthPicker month={month} onChange={onMonthChange} />
        <div className="toolbar__filter">
          <label htmlFor="category-filter">Category</label>
          <select id="category-filter" value={category} onChange={(e) => setCategory(e.target.value)}>
            <option value="">All categories</option>
            {CATEGORIES.map((c) => (
              <option key={c}>{c}</option>
            ))}
          </select>
        </div>
      </div>

      <section className="card" aria-labelledby="expenses-heading" aria-busy={loading}>
        <div className="card__header">
          <h2 id="expenses-heading">{filterLabel}</h2>
          {current?.length > 0 && (
            <p className="card__meta">
              {current.length} {current.length === 1 ? 'expense' : 'expenses'} ·{' '}
              <span className="amount">{formatPeso(current.reduce((sum, e) => sum + e.amount, 0))}</span>
            </p>
          )}
        </div>

        {error && !current ? (
          <ErrorState error={error} onRetry={reload} />
        ) : !current ? (
          <LoadingState label="Loading expenses…" />
        ) : current.length === 0 ? (
          <EmptyState
            title={`No ${category ? `${category} ` : ''}expenses in ${formatMonth(month)}`}
            message={category ? 'Try another category, or show all categories.' : 'Add an expense to start tracking this month.'}
            action={
              category ? (
                <button type="button" className="button button--secondary" onClick={() => setCategory('')}>
                  Show all categories
                </button>
              ) : (
                <button type="button" className="button button--primary" onClick={() => setEditing(null)}>
                  Add expense
                </button>
              )
            }
          />
        ) : (
          <ExpenseTable expenses={current} caption={filterLabel} onEdit={setEditing} onDelete={setDeleting} />
        )}
      </section>

      <ExpenseFormModal
        open={editing !== undefined}
        expense={editing ?? null}
        month={month}
        onClose={() => setEditing(undefined)}
        onSaved={() => {
          setEditing(undefined)
          reload()
        }}
      />
      <ConfirmDialog
        open={deleting !== null}
        title="Delete expense?"
        message={
          deleting &&
          `“${deleting.description}” (${formatPeso(deleting.amount)} on ${formatDate(deleting.expenseDate)}) will be permanently deleted.`
        }
        busy={deleteBusy}
        onConfirm={confirmDelete}
        onCancel={() => setDeleting(null)}
      />
    </>
  )
}
