import { useState } from 'react'
import { CategoryIcon } from '../common/CategoryBadge.jsx'
import Icon from '../common/Icon.jsx'
import { EmptyState, ErrorState, LoadingState } from '../common/States.jsx'
import PayModal from '../bills/PayModal.jsx'
import { STATUS_LABELS, STATUS_TONES, dueText } from '../bills/billText.js'
import { useAsync } from '../../hooks/useAsync.js'
import { api } from '../../services/api.js'
import { formatDate, formatPeso } from '../../utils/format.js'

// Unpaid due dates, overdue first. Reminders are shown here in the app only;
// PennyWise does not send emails or notifications.
export default function UpcomingBills({ refreshKey, onPaid }) {
  const { data, error, reload } = useAsync(() => api.getBillOccurrences(), [refreshKey])
  const [paying, setPaying] = useState(null)
  const unpaid = data?.filter((o) => o.status !== 'Paid').sort((a, b) => a.dueDate.localeCompare(b.dueDate))

  return (
    <section className="card" aria-labelledby="upcoming-bills-heading">
      <div className="card__header">
        <h2 id="upcoming-bills-heading">Upcoming bills</h2>
        <a className="button button--link" href="#/bills">Manage bills</a>
      </div>
      {error && !data ? (
        <ErrorState error={error} onRetry={reload} />
      ) : !data ? (
        <LoadingState label="Loading bills…" variant="rows" />
      ) : unpaid.length === 0 ? (
        <EmptyState compact title="No bills due" message="Add your regular bills to be reminded here before they are due." />
      ) : (
        <ul className="bill-list">
          {unpaid.slice(0, 4).map((o) => (
            <li key={`${o.billId}-${o.dueDate}`} className={`bill-row bill-row--${STATUS_TONES[o.status]}`}>
              <CategoryIcon category={o.category} shape="round" />
              <div className="bill-row__main">
                <span className="bill-row__name">{o.name}</span>
                <span className="bill-row__meta">
                  {formatDate(o.dueDate)} · {dueText(o)}
                </span>
              </div>
              <span className="bill-row__amount">{formatPeso(o.amount)}</span>
              <span className={`status-badge status-badge--${STATUS_TONES[o.status]}`}>
                {o.status === 'Overdue' && <Icon name="alert" size={12} />}
                {STATUS_LABELS[o.status]}
              </span>
              <button type="button" className="button button--secondary button--small" onClick={() => setPaying(o)}>
                Mark paid
              </button>
            </li>
          ))}
        </ul>
      )}
      <PayModal
        occurrence={paying}
        onClose={() => setPaying(null)}
        onSaved={() => {
          setPaying(null)
          onPaid()
        }}
      />
    </section>
  )
}
