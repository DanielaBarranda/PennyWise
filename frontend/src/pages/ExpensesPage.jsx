import { useState } from 'react'
import Icon from '../components/common/Icon.jsx'
import PageHeader from '../components/common/PageHeader.jsx'
import MonthPicker from '../components/common/MonthPicker.jsx'
import ConfirmDialog from '../components/common/ConfirmDialog.jsx'
import { EmptyState, ErrorState, LoadingState } from '../components/common/States.jsx'
import CategoryFilter from '../components/expenses/CategoryFilter.jsx'
import ExpenseTable from '../components/expenses/ExpenseTable.jsx'
import ExpenseFormModal from '../components/expenses/ExpenseFormModal.jsx'
import { useAsync } from '../hooks/useAsync.js'
import { useToast } from '../hooks/useToast.js'
import { api } from '../services/api.js'
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
      <PageHeader
        title="Expenses"
        subtitle="Every expense you have recorded, by month."
        controls={<MonthPicker month={month} onChange={onMonthChange} />}
      >
        <button type="button" className="button button--primary" onClick={() => setEditing(null)}>
          <Icon name="plus" />
          Add expense
        </button>
      </PageHeader>

      <section className="card card--flush" aria-labelledby="expenses-heading" aria-busy={loading}>
        <div className="card__header card__header--padded">
          <h2 id="expenses-heading">{filterLabel}</h2>
          {current?.length > 0 && (
            <p className="card__meta">
              {current.length} {current.length === 1 ? 'expense' : 'expenses'}, total{' '}
              <strong className="amount">{formatPeso(current.reduce((sum, e) => sum + e.amount, 0))}</strong>
            </p>
          )}
        </div>
        <div className="card__toolbar">
          <CategoryFilter value={category} onChange={setCategory} />
        </div>

        {error && !current ? (
          <div className="card__body">
            <ErrorState error={error} onRetry={reload} />
          </div>
        ) : !current ? (
          <div className="card__body">
            <LoadingState label="Loading expenses…" variant="rows" />
          </div>
        ) : current.length === 0 ? (
          <div className="card__body">
            <EmptyState
              title={`No ${category ? `${category} ` : ''}expenses in ${formatMonth(month)}`}
              message={
                category
                  ? 'Try another category, or show every category.'
                  : 'Record something you spent this month to start tracking.'
              }
              action={
                category ? (
                  <button type="button" className="button button--secondary" onClick={() => setCategory('')}>
                    Show all categories
                  </button>
                ) : (
                  <button type="button" className="button button--primary" onClick={() => setEditing(null)}>
                    <Icon name="plus" />
                    Add expense
                  </button>
                )
              }
            />
          </div>
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
        title="Delete this expense?"
        message={
          deleting &&
          `“${deleting.description}” (${formatPeso(deleting.amount)} on ${formatDate(deleting.expenseDate)}) will be removed permanently. This can’t be undone.`
        }
        confirmLabel="Delete expense"
        busy={deleteBusy}
        onConfirm={confirmDelete}
        onCancel={() => setDeleting(null)}
      />
    </>
  )
}
