import { EmptyState, ErrorState, LoadingState } from '../common/States.jsx'
import InsightList from '../insights/InsightList.jsx'
import { useAsync } from '../../hooks/useAsync.js'
import { useAuth } from '../../hooks/useAuth.js'
import { useDismissedInsights } from '../../hooks/useDismissedInsights.js'
import { api } from '../../services/api.js'

// The three most important insights for the month; the rest are on the Insights page.
export default function InsightsPreview({ month, refreshKey }) {
  const { user } = useAuth()
  const [dismissed, dismiss] = useDismissedInsights(user.id)
  const { data, error, reload } = useAsync(() => api.getInsights(month).then((list) => ({ month, list })), [month, refreshKey])
  const current = data?.month === month ? data.list : null
  const visible = current?.filter((i) => !dismissed.has(i.key))

  return (
    <section className="card" aria-labelledby="insights-preview-heading">
      <div className="card__header">
        <h2 id="insights-preview-heading">Insights</h2>
        {visible?.length > 3 && <a className="button button--link" href="#/insights">See all {visible.length}</a>}
      </div>
      {error && !current ? (
        <ErrorState error={error} onRetry={reload} />
      ) : !current ? (
        <LoadingState label="Loading insights…" variant="rows" />
      ) : visible.length === 0 ? (
        <EmptyState compact title="Nothing to point out" message="Notes about your spending, bills and goals appear here." />
      ) : (
        <InsightList insights={visible.slice(0, 3)} onDismiss={dismiss} />
      )}
    </section>
  )
}
