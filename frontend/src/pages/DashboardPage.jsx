import { useRef, useState } from 'react'
import Icon from '../components/common/Icon.jsx'
import PageHeader from '../components/common/PageHeader.jsx'
import PeriodPicker from '../components/common/PeriodPicker.jsx'
import PeriodSwitch from '../components/common/PeriodSwitch.jsx'
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
import { periodLabel, periodQuery } from '../utils/budgetPeriod.js'

function greeting() {
  const hour = new Date().getHours()
  if (hour < 12) return 'Good morning'
  if (hour < 18) return 'Good afternoon'
  return 'Good evening'
}

// Everything here is for one budget period: a day, a week or a month, as the
// period switch says. Nothing is converted from one period to another.
export default function DashboardPage({ period, start, onStartChange }) {
  // Bumped after any change, so the summary and every widget below reload together.
  const [refreshKey, setRefreshKey] = useState(0)
  const { data: summary, error, loading } = useAsync(() => api.getDashboardSummary(periodQuery(period, start)), [period, start, refreshKey])
  const reload = () => setRefreshKey((k) => k + 1)
  const [budgetOpen, setBudgetOpen] = useState(false)
  const [expenseOpen, setExpenseOpen] = useState(false)
  const categoriesHeadingRef = useRef(null)

  // Ignore a summary for the previously selected period while the new one loads.
  const current = summary?.period === period && summary?.start === start ? summary : null
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
        subtitle={`Here’s your financial overview for ${periodLabel(period, start)}.`}
        controls={
          <>
            <PeriodSwitch />
            <PeriodPicker period={period} start={start} onChange={onStartChange} />
          </>
        }
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
          <SummaryCards summary={current} period={period} onSetBudget={() => setBudgetOpen(true)} onShowCategories={showCategories} />
          <div className="dashboard__columns dashboard__columns--even">
            <AllowanceCard period={period} start={start} refreshKey={refreshKey} />
            <InsightsPreview period={period} start={start} refreshKey={refreshKey} />
          </div>
          <div className="dashboard__columns">
            <SpendingOverview summary={current} />
            <CategoryBreakdown summary={current} headingRef={categoriesHeadingRef} />
          </div>
          <div className="dashboard__columns dashboard__columns--even">
            <UpcomingBills refreshKey={refreshKey} onPaid={reload} />
            <SavingsPreview refreshKey={refreshKey} />
          </div>
          <RecentTransactions expenses={current.recentExpenses} period={period} onAdd={() => setExpenseOpen(true)} />
        </div>
      )}

      <BudgetFormModal
        open={budgetOpen}
        period={period}
        start={start}
        budget={current?.budget ?? null}
        onClose={() => setBudgetOpen(false)}
        onSaved={() => {
          setBudgetOpen(false)
          reload()
        }}
        onRemoved={() => {
          setBudgetOpen(false)
          reload()
        }}
      />
      <ExpenseFormModal
        open={expenseOpen}
        expense={null}
        month={start.slice(0, 7)}
        onClose={() => setExpenseOpen(false)}
        onSaved={() => {
          setExpenseOpen(false)
          reload()
        }}
      />
    </>
  )
}
