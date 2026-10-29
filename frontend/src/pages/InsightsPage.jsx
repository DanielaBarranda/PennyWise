import PageHeader from '../components/common/PageHeader.jsx'
import MonthPicker from '../components/common/MonthPicker.jsx'
import { EmptyState, ErrorState, LoadingState } from '../components/common/States.jsx'
import InsightList from '../components/insights/InsightList.jsx'
import { useAsync } from '../hooks/useAsync.js'
import { useAuth } from '../hooks/useAuth.js'
import { useDismissedInsights } from '../hooks/useDismissedInsights.js'
import { api } from '../services/api.js'
import { formatMonth } from '../utils/format.js'

export default function InsightsPage({ month, onMonthChange }) {
  const { user } = useAuth()
  const { data, error, reload } = useAsync(() => api.getInsights(month).then((list) => ({ month, list })), [month])
  const [dismissed, dismiss] = useDismissedInsights(user.id)
  const current = data?.month === month ? data.list : null
  const visible = current?.filter((i) => !dismissed.has(i.key))
  const hiddenCount = current ? current.length - visible.length : 0

  return (
    <>
      <PageHeader
        title="Insights"
        subtitle="Notes worked out from your own budget, expenses, bills and savings."
        controls={<MonthPicker month={month} onChange={onMonthChange} />}
      />
      <section className="card" aria-labelledby="insights-heading">
        <div className="card__header">
          <h2 id="insights-heading">{formatMonth(month)}</h2>
          {hiddenCount > 0 && <p className="card__meta">{hiddenCount} hidden</p>}
        </div>
        {error && !current ? (
          <ErrorState error={error} onRetry={reload} />
        ) : !current ? (
          <LoadingState label="Loading insights…" variant="rows" />
        ) : visible.length === 0 ? (
          <EmptyState
            compact
            title={current.length === 0 ? 'Nothing to point out' : 'All insights hidden'}
            message={
              current.length === 0
                ? 'Insights appear as you record expenses, set limits, add bills and save toward goals.'
                : 'Hidden insights come back if your situation changes.'
            }
          />
        ) : (
          <InsightList insights={visible} onDismiss={dismiss} />
        )}
        <p className="card__footnote">
          “From your records” notes are calculated from what you saved in PennyWise. “Suggestions” are ideas, not advice.
        </p>
      </section>
    </>
  )
}
