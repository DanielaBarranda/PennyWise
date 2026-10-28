import { useState } from 'react'
import PageHeader from '../components/common/PageHeader.jsx'
import MonthPicker from '../components/common/MonthPicker.jsx'
import { EmptyState, ErrorState, LoadingState } from '../components/common/States.jsx'
import SummaryCards from '../components/dashboard/SummaryCards.jsx'
import BudgetProgress from '../components/dashboard/BudgetProgress.jsx'
import BudgetAlerts from '../components/dashboard/BudgetAlerts.jsx'
import CategoryBreakdown from '../components/dashboard/CategoryBreakdown.jsx'
import RecentTransactions from '../components/dashboard/RecentTransactions.jsx'
import BudgetFormModal from '../components/dashboard/BudgetFormModal.jsx'
import ExpenseFormModal from '../components/expenses/ExpenseFormModal.jsx'
import { useAsync } from '../hooks/useAsync.js'
import { api } from '../services/api.js'
import { formatMonth } from '../utils/format.js'

export default function DashboardPage({ month, onMonthChange }) {
  const { data: summary, error, loading, reload } = useAsync(() => api.getDashboardSummary(month), [month])
  const [budgetOpen, setBudgetOpen] = useState(false)
  const [expenseOpen, setExpenseOpen] = useState(false)

  // Ignore a summary that belongs to the previously selected month while the new one loads.
  const current = summary?.month === month ? summary : null
  const hasBudget = current?.budget != null

  return (
    <>
      <PageHeader title="Dashboard" subtitle={`Your spending overview for ${formatMonth(month)}`}>
        <MonthPicker month={month} onChange={onMonthChange} />
        <button type="button" className="button button--secondary" onClick={() => setBudgetOpen(true)} disabled={!current}>
          {hasBudget ? 'Edit budget' : 'Set budget'}
        </button>
        <button type="button" className="button button--primary" onClick={() => setExpenseOpen(true)}>
          Add expense
        </button>
      </PageHeader>

      {error && !current ? (
        <ErrorState error={error} onRetry={reload} />
      ) : !current ? (
        <LoadingState label="Loading your dashboard…" />
      ) : (
        <div className="dashboard" aria-busy={loading}>
          {error && <ErrorState error={error} onRetry={reload} />}

          {hasBudget ? (
            <BudgetAlerts summary={current} />
          ) : (
            <div className="card">
              <EmptyState
                title={`No budget set for ${formatMonth(month)}`}
                message="Set a monthly budget to see how much you have left and get warnings before you overspend."
                action={
                  <button type="button" className="button button--primary" onClick={() => setBudgetOpen(true)}>
                    Set budget
                  </button>
                }
              />
            </div>
          )}

          <SummaryCards summary={current} />
          {hasBudget && <BudgetProgress summary={current} />}

          <div className="dashboard__columns">
            <CategoryBreakdown summary={current} />
            <RecentTransactions expenses={current.recentExpenses} onAdd={() => setExpenseOpen(true)} />
          </div>
        </div>
      )}

      <BudgetFormModal
        open={budgetOpen}
        month={month}
        budget={current?.budget ?? null}
        onClose={() => setBudgetOpen(false)}
        onSaved={() => {
          setBudgetOpen(false)
          reload()
        }}
      />
      <ExpenseFormModal
        open={expenseOpen}
        expense={null}
        month={month}
        onClose={() => setExpenseOpen(false)}
        onSaved={() => {
          setExpenseOpen(false)
          reload()
        }}
      />
    </>
  )
}
