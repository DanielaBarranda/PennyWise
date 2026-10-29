import { EmptyState, ErrorState, LoadingState } from '../common/States.jsx'
import { useAsync } from '../../hooks/useAsync.js'
import { api } from '../../services/api.js'
import { formatPeso } from '../../utils/format.js'

// Up to three active goals, closest to finished first.
export default function SavingsPreview({ refreshKey }) {
  const { data, error, reload } = useAsync(() => api.getSavingsGoals(), [refreshKey])
  const goals = data?.filter((g) => g.status === 'Active').sort((a, b) => b.percentSaved - a.percentSaved)

  return (
    <section className="card" aria-labelledby="savings-preview-heading">
      <div className="card__header">
        <h2 id="savings-preview-heading">Savings goals</h2>
        <a className="button button--link" href="#/savings">View savings</a>
      </div>
      {error && !data ? (
        <ErrorState error={error} onRetry={reload} />
      ) : !data ? (
        <LoadingState label="Loading goals…" variant="rows" />
      ) : goals.length === 0 ? (
        <EmptyState compact title="No active goals" message="Create a goal to watch your savings grow toward it." />
      ) : (
        <ul className="progress-list">
          {goals.slice(0, 3).map((g) => (
            <li key={g.id} className="saving-row">
              <div className="progress-row__line">
                <span className="progress-row__name">{g.name}</span>
                <span className="progress-row__amounts">{formatPeso(g.savedAmount)} of {formatPeso(g.targetAmount)}</span>
              </div>
              <div
                className="progress-row__track"
                role="progressbar"
                aria-label={`${g.name} progress`}
                aria-valuemin={0}
                aria-valuemax={100}
                aria-valuenow={Math.min(Math.round(g.percentSaved), 100)}
                aria-valuetext={`${g.percentSaved}% saved`}
              >
                <span className="progress-row__bar saving-row__bar" style={{ width: `${Math.min(g.percentSaved, 100)}%` }} />
              </div>
              <span className="progress-row__detail">{g.percentSaved}% saved · {formatPeso(g.remainingAmount)} to go</span>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
