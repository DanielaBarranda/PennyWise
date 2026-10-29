import { useState } from 'react'
import { CategoryIcon } from '../components/common/CategoryBadge.jsx'
import ConfirmDialog from '../components/common/ConfirmDialog.jsx'
import FormField from '../components/common/FormField.jsx'
import Icon from '../components/common/Icon.jsx'
import Modal from '../components/common/Modal.jsx'
import PageHeader from '../components/common/PageHeader.jsx'
import { EmptyState, ErrorState, LoadingState } from '../components/common/States.jsx'
import CategoryPicker from '../components/expenses/CategoryPicker.jsx'
import PayModal from '../components/bills/PayModal.jsx'
import { FREQUENCY_LABELS, STATUS_LABELS, STATUS_TONES, dueText } from '../components/bills/billText.js'
import { useAsync } from '../hooks/useAsync.js'
import { useToast } from '../hooks/useToast.js'
import { api } from '../services/api.js'
import { todayIso } from '../utils/dates.js'
import { formatDate, formatPeso } from '../utils/format.js'
import { validateAmount } from '../utils/validation.js'

const shift = (days) => {
  const d = new Date()
  d.setDate(d.getDate() + days)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

export default function BillsPage() {
  const toast = useToast()
  const [showArchived, setShowArchived] = useState(false)
  // Due dates from 60 days ago to 60 days ahead: overdue, upcoming and recently paid.
  const occurrences = useAsync(() => api.getBillOccurrences({ from: shift(-60), to: shift(60) }), [])
  const bills = useAsync(() => api.getBills(showArchived), [showArchived])
  const [editing, setEditing] = useState(undefined)
  const [paying, setPaying] = useState(null)
  const [undoing, setUndoing] = useState(null)
  const [undoBusy, setUndoBusy] = useState(false)

  const reloadAll = () => {
    occurrences.reload()
    bills.reload()
  }

  async function toggleArchive(bill) {
    try {
      if (bill.archived) await api.restoreBill(bill.id)
      else await api.archiveBill(bill.id)
      toast.success(bill.archived ? `Restored “${bill.name}”.` : `Archived “${bill.name}”. Its payment history is kept.`)
      reloadAll()
    } catch (err) {
      toast.error(err.message)
    }
  }

  async function confirmUndo() {
    setUndoBusy(true)
    try {
      await api.unpayBill(undoing.billId, undoing.dueDate)
      toast.success(`Marked “${undoing.name}” as unpaid.`)
      setUndoing(null)
      reloadAll()
    } catch (err) {
      toast.error(err.message)
    } finally {
      setUndoBusy(false)
    }
  }

  const list = occurrences.data ?? []
  const groups = ['Overdue', 'DueSoon', 'Upcoming', 'Paid']
    .map((status) => ({ status, items: list.filter((o) => o.status === status) }))
    .map((g) => (g.status === 'Paid' ? { ...g, items: [...g.items].reverse().slice(0, 10) } : g))
    .filter((g) => g.items.length > 0)

  return (
    <>
      <PageHeader title="Bills" subtitle="Keep track of what’s due and what’s already paid.">
        <button type="button" className="button button--primary" onClick={() => setEditing(null)}>
          <Icon name="plus" />
          New bill
        </button>
      </PageHeader>

      <p className="page-note">
        <Icon name="info" size={16} />
        Reminders appear inside PennyWise only, on the dashboard and in Insights. PennyWise doesn’t send email or phone notifications.
      </p>

      <section className="card" aria-labelledby="due-heading">
        <div className="card__header">
          <h2 id="due-heading">Due dates</h2>
          <p className="card__meta">Last 60 days and the next 60 days</p>
        </div>
        {occurrences.error && !occurrences.data ? (
          <ErrorState error={occurrences.error} onRetry={occurrences.reload} />
        ) : !occurrences.data ? (
          <LoadingState label="Loading due dates…" variant="rows" />
        ) : groups.length === 0 ? (
          <EmptyState compact title="No bills due" message="Add a bill and its due dates will appear here." />
        ) : (
          groups.map((g) => (
            <div key={g.status} className="bill-group">
              <h3 className="bill-group__title">
                {STATUS_LABELS[g.status]} <span className="bill-group__count">{g.items.length}</span>
              </h3>
              <ul className="bill-list">
                {g.items.map((o) => (
                  <li key={`${o.billId}-${o.dueDate}`} className={`bill-row bill-row--${STATUS_TONES[o.status]}`}>
                    <CategoryIcon category={o.category} shape="round" />
                    <div className="bill-row__main">
                      <span className="bill-row__name">{o.name}</span>
                      <span className="bill-row__meta">
                        {formatDate(o.dueDate)} · {dueText(o)}
                      </span>
                    </div>
                    <span className="bill-row__amount">{formatPeso(o.paidAmount ?? o.amount)}</span>
                    <span className={`status-badge status-badge--${STATUS_TONES[o.status]}`}>
                      {o.status === 'Overdue' && <Icon name="alert" size={12} />}
                      {o.status === 'Paid' && <Icon name="check" size={12} />}
                      {STATUS_LABELS[o.status]}
                    </span>
                    {o.status === 'Paid' ? (
                      <button type="button" className="button button--ghost button--small" onClick={() => setUndoing(o)}>
                        Undo
                      </button>
                    ) : (
                      <button type="button" className="button button--secondary button--small" onClick={() => setPaying(o)}>
                        Mark paid
                      </button>
                    )}
                  </li>
                ))}
              </ul>
            </div>
          ))
        )}
      </section>

      <section className="card card--flush" aria-labelledby="bills-heading">
        <div className="card__header card__header--padded">
          <h2 id="bills-heading">Your bills</h2>
          <label className="toggle toggle--inline">
            <input type="checkbox" checked={showArchived} onChange={(e) => setShowArchived(e.target.checked)} />
            Show archived
          </label>
        </div>
        {bills.error && !bills.data ? (
          <div className="card__body"><ErrorState error={bills.error} onRetry={bills.reload} /></div>
        ) : !bills.data ? (
          <div className="card__body"><LoadingState label="Loading bills…" variant="rows" /></div>
        ) : bills.data.length === 0 ? (
          <div className="card__body">
            <EmptyState
              compact
              title="No bills yet"
              message="Add rent, internet, tuition or anything you pay on a schedule."
              action={
                <button type="button" className="button button--primary" onClick={() => setEditing(null)}>
                  <Icon name="plus" />
                  Add a bill
                </button>
              }
            />
          </div>
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <caption className="visually-hidden">Your bills</caption>
              <thead>
                <tr>
                  <th scope="col">Bill</th>
                  <th scope="col">Repeats</th>
                  <th scope="col">Next due</th>
                  <th scope="col" className="num">Amount</th>
                  <th scope="col"><span className="visually-hidden">Actions</span></th>
                </tr>
              </thead>
              <tbody>
                {bills.data.map((b) => (
                  <tr key={b.id} className={b.archived ? 'is-archived' : undefined}>
                    <td>
                      <div className="data-table__cell">
                        <CategoryIcon category={b.category} size="sm" shape="round" />
                        <span className="data-table__text">
                          <span className="data-table__strong">{b.name}{b.archived && ' (archived)'}</span>
                          {b.notes && <span className="data-table__mobile-meta data-table__note">{b.notes}</span>}
                        </span>
                      </div>
                    </td>
                    <td className="data-table__muted">{FREQUENCY_LABELS[b.frequency]}</td>
                    <td className="data-table__muted">{b.nextDueDate ? formatDate(b.nextDueDate) : 'No more due dates'}</td>
                    <td className="num data-table__strong">{formatPeso(b.amount)}</td>
                    <td className="data-table__actions">
                      {!b.archived && (
                        <button type="button" className="icon-button" onClick={() => setEditing(b)} aria-label={`Edit ${b.name}`} title="Edit">
                          <Icon name="pencil" />
                        </button>
                      )}
                      <button
                        type="button"
                        className="icon-button"
                        onClick={() => toggleArchive(b)}
                        aria-label={b.archived ? `Restore ${b.name}` : `Archive ${b.name}`}
                        title={b.archived ? 'Restore' : 'Archive'}
                      >
                        <Icon name="archive" />
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <BillFormModal open={editing !== undefined} bill={editing ?? null} onClose={() => setEditing(undefined)}
        onSaved={() => { setEditing(undefined); reloadAll() }} />
      <PayModal occurrence={paying} onClose={() => setPaying(null)} onSaved={() => { setPaying(null); reloadAll() }} />
      <ConfirmDialog
        open={undoing !== null}
        title="Mark as unpaid?"
        message={undoing && (undoing.expenseId
          ? `This removes the payment for “${undoing.name}” due ${formatDate(undoing.dueDate)} and the expense PennyWise recorded for it.`
          : `This removes the payment for “${undoing.name}” due ${formatDate(undoing.dueDate)}.`)}
        confirmLabel="Mark as unpaid"
        busy={undoBusy}
        onConfirm={confirmUndo}
        onCancel={() => setUndoing(null)}
      />
    </>
  )
}

function BillFormModal({ open, bill, onClose, onSaved }) {
  return (
    <Modal open={open} title={bill ? 'Edit bill' : 'New bill'} onClose={onClose}>
      <BillForm bill={bill} onCancel={onClose} onSaved={onSaved} />
    </Modal>
  )
}

function BillForm({ bill, onCancel, onSaved }) {
  const toast = useToast()
  const [form, setForm] = useState({
    name: bill?.name ?? '',
    amount: bill ? String(bill.amount) : '',
    category: bill?.category ?? 'Bills',
    frequency: bill?.frequency ?? 'Monthly',
    firstDueDate: bill?.firstDueDate ?? todayIso(),
    endDate: bill?.endDate ?? '',
    notes: bill?.notes ?? '',
  })
  const [errors, setErrors] = useState({})
  const [busy, setBusy] = useState(false)
  const update = (field) => (e) => {
    setForm((f) => ({ ...f, [field]: e.target.value }))
    setErrors(({ [field]: _cleared, ...rest }) => rest)
  }

  async function handleSubmit(event) {
    event.preventDefault()
    const clientErrors = {}
    if (!form.name.trim()) clientErrors.name = 'Name is required.'
    const amountError = validateAmount(form.amount)
    if (amountError) clientErrors.amount = amountError
    if (!form.firstDueDate) clientErrors.firstDueDate = 'Due date is required.'
    if (form.endDate && form.endDate < form.firstDueDate) clientErrors.endDate = 'End date must be on or after the first due date.'
    if (Object.keys(clientErrors).length) return setErrors(clientErrors)

    const payload = {
      name: form.name.trim(),
      amount: Number(form.amount),
      category: form.category,
      frequency: form.frequency,
      firstDueDate: form.firstDueDate,
      endDate: form.frequency === 'OneTime' ? null : form.endDate || null,
      notes: form.notes.trim() || null,
    }
    setBusy(true)
    try {
      if (bill) await api.updateBill(bill.id, payload)
      else await api.createBill(payload)
      toast.success(bill ? 'Bill updated.' : 'Bill added.')
      onSaved()
    } catch (err) {
      setErrors(Object.keys(err.fieldErrors ?? {}).length ? err.fieldErrors : { form: err.message })
      setBusy(false)
    }
  }

  return (
    <form className="form" onSubmit={handleSubmit} noValidate>
      {errors.form && <p className="form__error" role="alert">{errors.form}</p>}
      <div className="form__row">
        <FormField label="Bill name" error={errors.name}>
          {(props) => <input {...props} type="text" maxLength={80} placeholder="e.g. Internet" value={form.name} onChange={update('name')} data-autofocus />}
        </FormField>
        <FormField label="Amount" error={errors.amount}>
          {(props) => (
            <div className="input-affix">
              <span className="input-affix__prefix" aria-hidden="true">₱</span>
              <input {...props} type="text" inputMode="decimal" placeholder="0.00" value={form.amount} onChange={update('amount')} />
            </div>
          )}
        </FormField>
      </div>
      <div className="form__row">
        <FormField label="Repeats" error={errors.frequency}>
          {(props) => (
            <select {...props} value={form.frequency} onChange={update('frequency')}>
              {Object.entries(FREQUENCY_LABELS).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
            </select>
          )}
        </FormField>
        <FormField label={form.frequency === 'OneTime' ? 'Due date' : 'First due date'} error={errors.firstDueDate}>
          {(props) => <input {...props} type="date" value={form.firstDueDate} onChange={update('firstDueDate')} />}
        </FormField>
      </div>
      {form.frequency !== 'OneTime' && (
        <FormField label="Last due date (optional)" error={errors.endDate} hint="Leave empty if it repeats with no end.">
          {(props) => <input {...props} type="date" min={form.firstDueDate} value={form.endDate} onChange={update('endDate')} />}
        </FormField>
      )}
      <CategoryPicker value={form.category} onChange={(category) => setForm((f) => ({ ...f, category }))} error={errors.category} />
      <FormField label="Notes (optional)" error={errors.notes}>
        {(props) => <input {...props} type="text" maxLength={200} placeholder="e.g. Account number ends in 4821" value={form.notes} onChange={update('notes')} />}
      </FormField>
      <div className="modal__actions">
        <button type="button" className="button button--ghost" onClick={onCancel} disabled={busy}>Cancel</button>
        <button type="submit" className="button button--primary" disabled={busy}>{busy ? 'Saving…' : bill ? 'Save changes' : 'Add bill'}</button>
      </div>
    </form>
  )
}
