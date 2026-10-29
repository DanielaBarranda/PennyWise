import { useState } from 'react'
import FormField from '../components/common/FormField.jsx'
import Icon from '../components/common/Icon.jsx'
import Modal from '../components/common/Modal.jsx'
import PageHeader from '../components/common/PageHeader.jsx'
import { EmptyState, ErrorState, LoadingState } from '../components/common/States.jsx'
import { useAsync } from '../hooks/useAsync.js'
import { useToast } from '../hooks/useToast.js'
import { api } from '../services/api.js'
import { todayIso } from '../utils/dates.js'
import { formatDate, formatPeso } from '../utils/format.js'
import { validateAmount } from '../utils/validation.js'

export default function SavingsPage() {
  const toast = useToast()
  const [showArchived, setShowArchived] = useState(false)
  const { data: goals, error, loading, reload } = useAsync(() => api.getSavingsGoals(showArchived), [showArchived])
  const [editing, setEditing] = useState(undefined) // undefined = closed, null = new goal, object = edit
  const [moving, setMoving] = useState(null) // { goal, type }
  const [history, setHistory] = useState(null)

  async function toggleArchive(goal) {
    try {
      if (goal.status === 'Archived') await api.restoreSavingsGoal(goal.id)
      else await api.archiveSavingsGoal(goal.id)
      toast.success(goal.status === 'Archived' ? `Restored “${goal.name}”.` : `Archived “${goal.name}”.`)
      reload()
    } catch (err) {
      toast.error(err.message)
    }
  }

  return (
    <>
      <PageHeader title="Savings goals" subtitle="Set money aside for the things that matter to you.">
        <button type="button" className="button button--primary" onClick={() => setEditing(null)}>
          <Icon name="plus" />
          New goal
        </button>
      </PageHeader>

      <p className="page-note">
        <Icon name="info" size={16} />
        PennyWise records the money you set aside for each goal. It doesn’t move money between bank accounts, and saving
        doesn’t count as spending in your budget.
      </p>

      <label className="toggle">
        <input type="checkbox" checked={showArchived} onChange={(e) => setShowArchived(e.target.checked)} />
        Show archived goals
      </label>

      {error && !goals ? (
        <ErrorState error={error} onRetry={reload} />
      ) : !goals ? (
        <LoadingState label="Loading goals…" variant="card" />
      ) : goals.length === 0 ? (
        <div className="card">
          <EmptyState
            title="No savings goals yet"
            message="Create a goal, then record what you set aside for it."
            action={
              <button type="button" className="button button--primary" onClick={() => setEditing(null)}>
                <Icon name="plus" />
                Create a goal
              </button>
            }
          />
        </div>
      ) : (
        <div className="goal-grid" aria-busy={loading}>
          {goals.map((goal) => (
            <GoalCard
              key={goal.id}
              goal={goal}
              onDeposit={() => setMoving({ goal, type: 'Contribution' })}
              onWithdraw={() => setMoving({ goal, type: 'Withdrawal' })}
              onHistory={() => setHistory(goal)}
              onEdit={() => setEditing(goal)}
              onArchive={() => toggleArchive(goal)}
            />
          ))}
        </div>
      )}

      <GoalFormModal
        open={editing !== undefined}
        goal={editing ?? null}
        onClose={() => setEditing(undefined)}
        onSaved={() => {
          setEditing(undefined)
          reload()
        }}
      />
      <MoneyModal
        open={moving !== null}
        goal={moving?.goal}
        type={moving?.type}
        onClose={() => setMoving(null)}
        onSaved={() => {
          setMoving(null)
          reload()
        }}
      />
      <HistoryModal goal={history} onClose={() => setHistory(null)} />
    </>
  )
}

const STATUS = { Active: 'In progress', Completed: 'Goal reached', Archived: 'Archived' }

function GoalCard({ goal, onDeposit, onWithdraw, onHistory, onEdit, onArchive }) {
  const archived = goal.status === 'Archived'
  const pace =
    goal.expectedByToday == null || goal.status !== 'Active'
      ? null
      : goal.savedAmount >= goal.expectedByToday
        ? { tone: 'ok', text: 'On pace for the target date' }
        : { tone: 'warning', text: `${formatPeso(goal.expectedByToday - goal.savedAmount)} behind an even pace` }

  return (
    <article className={`card goal goal--${goal.status.toLowerCase()}`} aria-labelledby={`goal-${goal.id}`}>
      <div className="goal__head">
        <h2 id={`goal-${goal.id}`}>{goal.name}</h2>
        <span className={`status-badge status-badge--${goal.status === 'Completed' ? 'ok' : archived ? 'muted' : 'info'}`}>
          {goal.status === 'Completed' && <Icon name="check" size={12} />}
          {STATUS[goal.status]}
        </span>
      </div>
      {goal.description && <p className="goal__description">{goal.description}</p>}

      <p className="goal__amounts">
        <span className="goal__saved">{formatPeso(goal.savedAmount)}</span>
        <span className="goal__target">of {formatPeso(goal.targetAmount)}</span>
      </p>
      <div
        className="goal__track"
        role="progressbar"
        aria-label={`${goal.name} progress`}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={Math.min(Math.round(goal.percentSaved), 100)}
        aria-valuetext={`${goal.percentSaved}% saved`}
      >
        <span className="goal__bar" style={{ width: `${Math.min(goal.percentSaved, 100)}%` }} />
      </div>
      <dl className="goal__facts">
        <div>
          <dt>Saved</dt>
          <dd>{goal.percentSaved}%</dd>
        </div>
        <div>
          <dt>Still to go</dt>
          <dd>{formatPeso(goal.remainingAmount)}</dd>
        </div>
        <div>
          <dt>Target date</dt>
          <dd>
            {goal.targetDate ? formatDate(goal.targetDate) : 'None'}
            {goal.daysLeft != null && goal.status === 'Active' && (
              <span className="goal__days">{goal.daysLeft >= 0 ? `${goal.daysLeft} days left` : 'Date passed'}</span>
            )}
          </dd>
        </div>
      </dl>
      {pace && <p className={`goal__pace goal__pace--${pace.tone}`}>{pace.text}</p>}

      <div className="goal__actions">
        {!archived && (
          <>
            <button type="button" className="button button--primary button--small" onClick={onDeposit}>
              <Icon name="plus" size={16} />
              Add money
            </button>
            <button type="button" className="button button--secondary button--small" onClick={onWithdraw} disabled={goal.savedAmount <= 0}>
              Withdraw
            </button>
          </>
        )}
        <button type="button" className="button button--ghost button--small" onClick={onHistory}>
          History
        </button>
        {!archived && (
          <button type="button" className="icon-button" onClick={onEdit} aria-label={`Edit ${goal.name}`} title="Edit">
            <Icon name="pencil" />
          </button>
        )}
        <button
          type="button"
          className="icon-button"
          onClick={onArchive}
          aria-label={archived ? `Restore ${goal.name}` : `Archive ${goal.name}`}
          title={archived ? 'Restore' : 'Archive'}
        >
          <Icon name="archive" />
        </button>
      </div>
    </article>
  )
}

function GoalFormModal({ open, goal, onClose, onSaved }) {
  return (
    <Modal open={open} title={goal ? 'Edit goal' : 'New savings goal'} onClose={onClose}>
      <GoalForm goal={goal} onCancel={onClose} onSaved={onSaved} />
    </Modal>
  )
}

function GoalForm({ goal, onCancel, onSaved }) {
  const toast = useToast()
  const [form, setForm] = useState({
    name: goal?.name ?? '',
    description: goal?.description ?? '',
    targetAmount: goal ? String(goal.targetAmount) : '',
    targetDate: goal?.targetDate ?? '',
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
    const amountError = validateAmount(form.targetAmount, { label: 'Target amount' })
    if (amountError) clientErrors.targetAmount = amountError
    if (Object.keys(clientErrors).length) return setErrors(clientErrors)

    const payload = {
      name: form.name.trim(),
      description: form.description.trim() || null,
      targetAmount: Number(form.targetAmount),
      targetDate: form.targetDate || null,
    }
    setBusy(true)
    try {
      if (goal) await api.updateSavingsGoal(goal.id, payload)
      else await api.createSavingsGoal(payload)
      toast.success(goal ? 'Goal updated.' : 'Goal created.')
      onSaved()
    } catch (err) {
      setErrors(Object.keys(err.fieldErrors ?? {}).length ? err.fieldErrors : { form: err.message })
      setBusy(false)
    }
  }

  return (
    <form className="form" onSubmit={handleSubmit} noValidate>
      {errors.form && <p className="form__error" role="alert">{errors.form}</p>}
      <FormField label="Goal name" error={errors.name}>
        {(props) => <input {...props} type="text" maxLength={80} placeholder="e.g. New laptop" value={form.name} onChange={update('name')} data-autofocus />}
      </FormField>
      <div className="form__row">
        <FormField label="Target amount" error={errors.targetAmount}>
          {(props) => (
            <div className="input-affix">
              <span className="input-affix__prefix" aria-hidden="true">₱</span>
              <input {...props} type="text" inputMode="decimal" placeholder="0.00" value={form.targetAmount} onChange={update('targetAmount')} />
            </div>
          )}
        </FormField>
        <FormField label="Target date (optional)" error={errors.targetDate}>
          {(props) => <input {...props} type="date" min={goal ? undefined : todayIso()} value={form.targetDate} onChange={update('targetDate')} />}
        </FormField>
      </div>
      <FormField label="Description (optional)" error={errors.description}>
        {(props) => <input {...props} type="text" maxLength={200} value={form.description} onChange={update('description')} />}
      </FormField>
      <div className="modal__actions">
        <button type="button" className="button button--ghost" onClick={onCancel} disabled={busy}>Cancel</button>
        <button type="submit" className="button button--primary" disabled={busy}>{busy ? 'Saving…' : goal ? 'Save changes' : 'Create goal'}</button>
      </div>
    </form>
  )
}

function MoneyModal({ open, goal, type, onClose, onSaved }) {
  const deposit = type === 'Contribution'
  return (
    <Modal
      open={open}
      title={deposit ? `Add money to ${goal?.name ?? ''}` : `Withdraw from ${goal?.name ?? ''}`}
      description={goal ? `${formatPeso(goal.savedAmount)} saved so far.` : undefined}
      onClose={onClose}
    >
      {goal && <MoneyForm goal={goal} type={type} onCancel={onClose} onSaved={onSaved} />}
    </Modal>
  )
}

function MoneyForm({ goal, type, onCancel, onSaved }) {
  const toast = useToast()
  const deposit = type === 'Contribution'
  const [form, setForm] = useState({ amount: '', date: todayIso(), note: '' })
  const [errors, setErrors] = useState({})
  const [busy, setBusy] = useState(false)
  const update = (field) => (e) => {
    setForm((f) => ({ ...f, [field]: e.target.value }))
    setErrors(({ [field]: _cleared, ...rest }) => rest)
  }

  async function handleSubmit(event) {
    event.preventDefault()
    const amountError = validateAmount(form.amount)
    if (amountError) return setErrors({ amount: amountError })
    if (!deposit && Number(form.amount) > goal.savedAmount) {
      return setErrors({ amount: `You can withdraw at most ${formatPeso(goal.savedAmount)}.` })
    }
    setBusy(true)
    try {
      const updated = await api.addSavingsTransaction(goal.id, { type, amount: Number(form.amount), date: form.date, note: form.note.trim() || null })
      toast.success(
        updated.status === 'Completed' && deposit
          ? `You reached your ${goal.name} goal!`
          : deposit
            ? `Added ${formatPeso(Number(form.amount))} to ${goal.name}.`
            : `Withdrew ${formatPeso(Number(form.amount))} from ${goal.name}.`,
      )
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
        <FormField label="Amount" error={errors.amount}>
          {(props) => (
            <div className="input-affix input-affix--large">
              <span className="input-affix__prefix" aria-hidden="true">₱</span>
              <input {...props} type="text" inputMode="decimal" placeholder="0.00" value={form.amount} onChange={update('amount')} data-autofocus />
            </div>
          )}
        </FormField>
        <FormField label="Date" error={errors.date}>
          {(props) => <input {...props} type="date" max={todayIso()} value={form.date} onChange={update('date')} />}
        </FormField>
      </div>
      <FormField label="Note (optional)" error={errors.note}>
        {(props) => <input {...props} type="text" maxLength={120} placeholder={deposit ? 'e.g. From allowance' : 'e.g. Paid the deposit'} value={form.note} onChange={update('note')} />}
      </FormField>
      <div className="modal__actions">
        <button type="button" className="button button--ghost" onClick={onCancel} disabled={busy}>Cancel</button>
        <button type="submit" className="button button--primary" disabled={busy}>{busy ? 'Saving…' : deposit ? 'Add money' : 'Withdraw'}</button>
      </div>
    </form>
  )
}

function HistoryModal({ goal, onClose }) {
  return (
    <Modal open={goal !== null} title={goal ? `${goal.name} history` : ''} onClose={onClose} wide>
      {goal && <History goalId={goal.id} />}
    </Modal>
  )
}

function History({ goalId }) {
  const { data, error } = useAsync(() => api.getSavingsTransactions(goalId), [goalId])
  if (error) return <div className="form"><ErrorState error={error} /></div>
  if (!data) return <div className="form"><LoadingState label="Loading history…" variant="rows" /></div>
  if (data.length === 0) {
    return <div className="form"><EmptyState compact title="No deposits yet" message="Money you add or withdraw appears here." /></div>
  }
  return (
    <div className="table-wrap history">
      <table className="data-table data-table--keep-head">
        <caption className="visually-hidden">Deposits and withdrawals</caption>
        <thead>
          <tr>
            <th scope="col">Date</th>
            <th scope="col">Type</th>
            <th scope="col">Note</th>
            <th scope="col" className="num">Amount</th>
          </tr>
        </thead>
        <tbody>
          {data.map((t) => (
            <tr key={t.id}>
              <td className="data-table__muted">{formatDate(t.date)}</td>
              <td>{t.type === 'Contribution' ? 'Deposit' : 'Withdrawal'}</td>
              <td className="data-table__muted">{t.note ?? '—'}</td>
              <td className={`num data-table__strong ${t.type === 'Withdrawal' ? 'amount--out' : 'amount--in'}`}>
                {t.type === 'Withdrawal' ? '−' : '+'}
                {formatPeso(t.amount)}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
