import { EmptyState, ErrorState, LoadingState } from '../common/States.jsx'
import { useAsync } from '../../hooks/useAsync.js'
import { api } from '../../services/api.js'
import { formatPeso } from '../../utils/format.js'

const SHOWN = 3

// Total savings, then up to three goals and funds: goals closest to finished
// first, then funds (no target) with the most money.
export default function SavingsPreview({ refreshKey }) {
  const { data, error, reload } = useAsync(() => api.getSavingsGoals(), [refreshKey])
  const active = data?.filter((g) => g.status !== 'Archived') ?? []
  const total = active.reduce((sum, g) => sum + Math.round(g.savedAmount * 100), 0) / 100
  const goals = active.filter((g) => g.targetAmount != null && g.status === 'Active').sort((a, b) => b.percentSaved - a.percentSaved)
  const funds = active.filter((g) => g.targetAmount == null).sort((a, b) => b.savedAmount - a.savedAmount)
  const shown = [...goals, ...funds].slice(0, SHOWN)

  return (
    <section className="card" aria-labelledby="savings-preview-heading">
      <div className="card__header">
        <h2 id="savings-preview-heading">Savings</h2>
        <a className="button button--link" href="#/savings">View savings</a>
      </div>
      {error && !data ? (
        <ErrorState error={error} onRetry={reload} />
      ) : !data ? (
        <LoadingState label="Loading savings…" variant="rows" />
      ) : active.length === 0 ? (
        <EmptyState compact title="No savings yet" message="Add a goal, or a place you keep money like a wallet or an envelope." />
      ) : (
        <>
          <p className="savings-preview__total">
            <span>Total savings</span>
            <strong>{formatPeso(total)}</strong>
          </p>
          <ul className="progress-list">
            {shown.map((g) =>
              g.targetAmount == null ? (
                <li key={g.id} className="saving-row saving-row--fund">
                  <div className="progress-row__line">
                    <span className="progress-row__name">{g.name}</span>
                    <span className="progress-row__amounts">{formatPeso(g.savedAmount)}</span>
                  </div>
                </li>
              ) : (
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
              ),
            )}
          </ul>
        </>
      )}
    </section>
  )
}
