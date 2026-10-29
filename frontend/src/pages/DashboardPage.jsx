import { useRef, useState } from 'react'
import Icon from '../components/common/Icon.jsx'
import PageHeader from '../components/common/PageHeader.jsx'
import MonthPicker from '../components/common/MonthPicker.jsx'
import { ErrorState, LoadingState } from '../components/common/States.jsx'
import SummaryCards from '../components/dashboard/SummaryCards.jsx'
import SpendingOverview from '../components/dashboard/SpendingOverview.jsx'
import CategoryBreakdown from '../components/dashboard/CategoryBreakdown.jsx'
import RecentTransactions from '../components/dashboard/RecentTransactions.jsx'
import AllowanceCard from '../components/dashboard/AllowanceCard.jsx'
import InsightsPreview from '../components/dashboard/InsightsPreview.jsx'
import UpcomingBills from '../components/dashboard/UpcomingBills.jsx'
import SavingsPreview from '../components/dashboard/SavingsPreview.jsx'
import BudgetFormModal from '../components/dashboard/BudgetFormModal.jsx'
import ExpenseFormModal from '../components/expenses/ExpenseFormModal.jsx'
import { useAsync } from '../hooks/useAsync.js'
import { api } from '../services/api.js'
import { formatMonth } from '../utils/format.js'

function greeting() {
  const hour = new Date().getHours()
  if (hour < 12) return 'Good morning'
  if (hour < 18) return 'Good afternoon'
  return 'Good evening'
}

export default function DashboardPage({ month, onMonthChange }) {
  // Bumped after any change, so the summary and every widget below reload together.
  const [refreshKey, setRefreshKey] = useState(0)
  const { data: summary, error, loading } = useAsync(() => api.getDashboardSummary(month), [month, refreshKey])
  const reload = () => setRefreshKey((k) => k + 1)
  const [budgetOpen, setBudgetOpen] = useState(false)
  const [expenseOpen, setExpenseOpen] = useState(false)
  const categoriesHeadingRef = useRef(null)

  // Ignore a summary that belongs to the previously selected month while the new one loads.
  const current = summary?.month === month ? summary : null
  const hasBudget = current?.budget != null

  function showCategories() {
    const heading = categoriesHeadingRef.current
    heading?.scrollIntoView({ behavior: 'smooth', block: 'start' })
    heading?.focus({ preventScroll: true })
  }

  return (
    <>
      <PageHeader
        title={greeting()}
        docTitle="Dashboard"
        subtitle={`Here’s your financial overview for ${formatMonth(month)}.`}
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
          <LoadingState label="Loading your dashboard…" variant="stats" />
          <div className="dashboard__columns">
            <LoadingState label="" variant="card" />
            <LoadingState label="" variant="card" />
          </div>
        </div>
      ) : (
        <div className="dashboard" aria-busy={loading}>
          {error && <ErrorState error={error} onRetry={reload} />}
          <SummaryCards summary={current} onSetBudget={() => setBudgetOpen(true)} onShowCategories={showCategories} />
          <div className="dashboard__columns dashboard__columns--even">
            <AllowanceCard month={month} refreshKey={refreshKey} />
            <InsightsPreview month={month} refreshKey={refreshKey} />
          </div>
          <div className="dashboard__columns">
            <SpendingOverview summary={current} />
            <CategoryBreakdown summary={current} headingRef={categoriesHeadingRef} />
          </div>
          <div className="dashboard__columns dashboard__columns--even">
            <UpcomingBills refreshKey={refreshKey} onPaid={reload} />
            <SavingsPreview refreshKey={refreshKey} />
          </div>
          <RecentTransactions expenses={current.recentExpenses} onAdd={() => setExpenseOpen(true)} />
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
