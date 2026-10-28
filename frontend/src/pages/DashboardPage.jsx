import { useState } from 'react'
import Icon from '../components/common/Icon.jsx'
import PageHeader from '../components/common/PageHeader.jsx'
import MonthPicker from '../components/common/MonthPicker.jsx'
import { ErrorState, LoadingState } from '../components/common/States.jsx'
import BudgetOverview from '../components/dashboard/BudgetOverview.jsx'
import BudgetAlerts from '../components/dashboard/BudgetAlerts.jsx'
import CategoryBreakdown from '../components/dashboard/CategoryBreakdown.jsx'
import RecentTransactions from '../components/dashboard/RecentTransactions.jsx'
import BudgetFormModal from '../components/dashboard/BudgetFormModal.jsx'
import ExpenseFormModal from '../components/expenses/ExpenseFormModal.jsx'
import { useAsync } from '../hooks/useAsync.js'
import { api } from '../services/api.js'

export default function DashboardPage({ month, onMonthChange }) {
  const { data: summary, error, loading, reload } = useAsync(() => api.getDashboardSummary(month), [month])
  const [budgetOpen, setBudgetOpen] = useState(false)
  const [expenseOpen, setExpenseOpen] = useState(false)

  // Ignore a summary that belongs to the previously selected month while the new one loads.
  const current = summary?.month === month ? summary : null
  const hasBudget = current?.budget != null

  return (
    <>
      <PageHeader
        title="Dashboard"
        subtitle="Where your money went this month, and how much is left."
        controls={<MonthPicker month={month} onChange={onMonthChange} />}
      >
        <button type="button" className="button button--secondary" onClick={() => setBudgetOpen(true)} disabled={!current}>
          <Icon name="pencil" />
          {hasBudget ? 'Edit budget' : 'Set budget'}
        </button>
        <button type="button" className="button button--primary" onClick={() => setExpenseOpen(true)}>
          <Icon name="plus" />
          Add expense
        </button>
      </PageHeader>

      {error && !current ? (
        <ErrorState error={error} onRetry={reload} />
      ) : !current ? (
        <div className="dashboard">
          <LoadingState label="Loading your dashboard…" variant="overview" />
          <div className="dashboard__columns">
            <LoadingState label="" variant="card" />
            <LoadingState label="" variant="card" />
          </div>
        </div>
      ) : (
        <div className="dashboard" aria-busy={loading}>
          {error && <ErrorState error={error} onRetry={reload} />}
          <BudgetOverview summary={current} onSetBudget={() => setBudgetOpen(true)} />
          {hasBudget && <BudgetAlerts summary={current} />}
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
