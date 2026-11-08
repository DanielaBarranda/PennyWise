import { useState } from 'react'
import FormField from '../components/common/FormField.jsx'
import Icon from '../components/common/Icon.jsx'
import Modal from '../components/common/Modal.jsx'
import PageHeader from '../components/common/PageHeader.jsx'
import { EmptyState, ErrorState, LoadingState } from '../components/common/States.jsx'
import { useAsync } from '../hooks/useAsync.js'
import { useToast } from '../hooks/useToast.js'
import { api } from '../services/api.js'
import { CATEGORIES } from '../utils/constants.js'
import { todayIso } from '../utils/dates.js'
import { formatDate, formatPeso } from '../utils/format.js'
import { validateAmount } from '../utils/validation.js'

// Goals (with a target) and funds the user names, like "Wallet" or "Envelope"
// (no target, they only keep a balance). Every amount comes from the API.
export default function SavingsPage() {
  const toast = useToast()
  const [showArchived, setShowArchived] = useState(false)
  const { data: goals, error, loading, reload } = useAsync(() => api.getSavingsGoals(showArchived), [showArchived])
  const [editing, setEditing] = useState(undefined) // undefined = closed, null = new, object = edit
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
      <PageHeader title="Savings" subtitle="Your goals and the places you keep money, like a wallet or an envelope." actionsTour="savings-add">
        <button type="button" className="button button--primary" onClick={() => setEditing(null)}>
          <Icon name="plus" />
          New savings
        </button>
      </PageHeader>

      <p className="page-note">
        <Icon name="info" size={16} />
        PennyWise records your savings; it doesn’t move money between bank accounts. When you add money, choose whether it
        comes from your budget (it comes out of that day, week or month) or from somewhere else (your budget isn’t
        touched). Money you spend from savings can be added as an expense that doesn’t use your budget.
      </p>

      <label className="toggle">
        <input type="checkbox" checked={showArchived} onChange={(e) => setShowArchived(e.target.checked)} />
        Show archived
      </label>

      {error && !goals ? (
        <ErrorState error={error} onRetry={reload} />
      ) : !goals ? (
        <LoadingState label="Loading savings…" variant="card" />
      ) : goals.length === 0 ? (
        <div className="card" data-tour="savings-empty">
          <EmptyState
            title="No savings yet"
            message="Add a goal with a target, or a place you keep money, like a wallet or an envelope."
            action={
              <button type="button" className="button button--primary" onClick={() => setEditing(null)}>
                <Icon name="plus" />
                Add savings
              </button>
            }
          />
        </div>
      ) : (
        <>
          <SavingsTotal goals={goals} />
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
        </>
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

// The sum of every goal and fund that isn't archived.
function SavingsTotal({ goals }) {
  const active = goals.filter((g) => g.status !== 'Archived')
  const total = active.reduce((sum, g) => sum + Math.round(g.savedAmount * 100), 0) / 100
  const funds = active.filter((g) => g.targetAmount == null).length
  const withTarget = active.length - funds
  const parts = [
    withTarget > 0 && `${withTarget} ${withTarget === 1 ? 'goal' : 'goals'}`,
    funds > 0 && `${funds} ${funds === 1 ? 'fund' : 'funds'}`,
  ].filter(Boolean)
  return (
    <section className="savings-total" aria-label="Total savings">
      <span className="savings-total__icon" aria-hidden="true">
        <Icon name="piggy" size={22} />
      </span>
      <div>
        <p className="savings-total__label">Total savings</p>
        <p className="savings-total__value">{formatPeso(total)}</p>
        {parts.length > 0 && <p className="savings-total__note">Across {parts.join(' and ')}</p>}
      </div>
    </section>
  )
}

function GoalCard({ goal, onDeposit, onWithdraw, onHistory, onEdit, onArchive }) {
  const archived = goal.status === 'Archived'
  const fund = goal.targetAmount == null
  const pace =
    goal.expectedByToday == null || goal.status !== 'Active'
      ? null
      : goal.savedAmount >= goal.expectedByToday
        ? { tone: 'ok', text: 'On pace for the target date' }
        : { tone: 'warning', text: `${formatPeso(goal.expectedByToday - goal.savedAmount)} behind an even pace` }

  return (
    <article className={`card goal goal--${goal.status.toLowerCase()}${fund ? ' goal--fund' : ''}`} aria-labelledby={`goal-${goal.id}`} data-tour="savings-goal">
      <div className="goal__head">
        <h2 id={`goal-${goal.id}`}>{goal.name}</h2>
        <span className={`status-badge status-badge--${goal.status === 'Completed' ? 'ok' : archived ? 'muted' : 'info'}`}>
          {goal.status === 'Completed' && <Icon name="check" size={12} />}
          {fund && !archived ? 'Fund' : STATUS[goal.status]}
        </span>
      </div>
      {goal.description && <p className="goal__description">{goal.description}</p>}

      {fund ? (
        <>
          <p className="goal__amounts">
            <span className="goal__saved">{formatPeso(goal.savedAmount)}</span>
          </p>
          <p className="goal__fund-note">No target. Add or take out money anytime.</p>
        </>
      ) : (
        <>
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
        </>
      )}

      <div className="goal__actions">
        {!archived && (
          <>
            <button type="button" className="button button--primary button--small" onClick={onDeposit}>
              <Icon name="plus" size={16} />
              Add money
            </button>
            <button type="button" className="button button--secondary button--small" onClick={onWithdraw} disabled={goal.savedAmount <= 0}>
              Take out
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
    <Modal open={open} title={goal ? `Edit ${goal.name}` : 'New savings'} onClose={onClose}>
      <GoalForm goal={goal} onCancel={onClose} onSaved={onSaved} />
    </Modal>
  )
}

function GoalForm({ goal, onCancel, onSaved }) {
  const toast = useToast()
  const [form, setForm] = useState({
    name: goal?.name ?? '',
    description: goal?.description ?? '',
    targetAmount: goal?.targetAmount != null ? String(goal.targetAmount) : '',
    targetDate: goal?.targetDate ?? '',
    startingAmount: '',
  })
  const [errors, setErrors] = useState({})
  const [busy, setBusy] = useState(false)
  const update = (field) => (e) => {
    setForm((f) => ({ ...f, [field]: e.target.value }))
    setErrors(({ [field]: _cleared, ...rest }) => rest)
  }
  const hasTarget = form.targetAmount.trim() !== ''

  async function handleSubmit(event) {
    event.preventDefault()
    const clientErrors = {}
    if (!form.name.trim()) clientErrors.name = 'Name is required.'
    const amountError = hasTarget ? validateAmount(form.targetAmount, { label: 'Target amount' }) : null
    if (amountError) clientErrors.targetAmount = amountError
    const startingError = form.startingAmount.trim() !== '' ? validateAmount(form.startingAmount, { label: 'Amount' }) : null
    if (startingError) clientErrors.startingAmount = startingError
    if (Object.keys(clientErrors).length) return setErrors(clientErrors)

    const payload = {
      name: form.name.trim(),
      description: form.description.trim() || null,
      targetAmount: hasTarget ? Number(form.targetAmount) : null,
      targetDate: hasTarget ? form.targetDate || null : null,
    }
    if (!goal && form.startingAmount.trim() !== '') payload.startingAmount = Number(form.startingAmount)
    setBusy(true)
    try {
      if (goal) await api.updateSavingsGoal(goal.id, payload)
      else await api.createSavingsGoal(payload)
      toast.success(goal ? `Saved ${payload.name}.` : `Created ${payload.name}.`)
      onSaved()
    } catch (err) {
      setErrors(Object.keys(err.fieldErrors ?? {}).length ? err.fieldErrors : { form: err.message })
      setBusy(false)
    }
  }

  return (
    <form className="form" onSubmit={handleSubmit} noValidate>
      {errors.form && <p className="form__error" role="alert">{errors.form}</p>}
      <FormField label="Name" error={errors.name}>
        {(props) => <input {...props} type="text" maxLength={80} placeholder="e.g. New laptop, Wallet, Envelope" value={form.name} onChange={update('name')} data-autofocus />}
      </FormField>
      <div className="form__row">
        <FormField label="Target amount (optional)" error={errors.targetAmount} hint="Leave empty for a place that just keeps money">
          {(props) => (
            <div className="input-affix">
              <span className="input-affix__prefix" aria-hidden="true">₱</span>
              <input {...props} type="text" inputMode="decimal" placeholder="0.00" value={form.targetAmount} onChange={update('targetAmount')} />
            </div>
          )}
        </FormField>
        <FormField label="Target date (optional)" error={errors.targetDate} hint={hasTarget ? undefined : 'Needs a target amount'}>
          {(props) => <input {...props} type="date" min={goal ? undefined : todayIso()} value={form.targetDate} onChange={update('targetDate')} disabled={!hasTarget} />}
        </FormField>
      </div>
      {!goal && (
        <FormField label="Money already in it (optional)" error={errors.startingAmount} hint="Doesn’t come out of your budget">
          {(props) => (
            <div className="input-affix">
              <span className="input-affix__prefix" aria-hidden="true">₱</span>
              <input {...props} type="text" inputMode="decimal" placeholder="0.00" value={form.startingAmount} onChange={update('startingAmount')} />
            </div>
          )}
        </FormField>
      )}
      <FormField label="Description (optional)" error={errors.description}>
        {(props) => <input {...props} type="text" maxLength={200} value={form.description} onChange={update('description')} />}
      </FormField>
      <div className="modal__actions">
        <button type="button" className="button button--ghost" onClick={onCancel} disabled={busy}>Cancel</button>
        <button type="submit" className="button button--primary" disabled={busy}>{busy ? 'Saving…' : goal ? 'Save changes' : 'Create'}</button>
      </div>
    </form>
  )
}

function MoneyModal({ open, goal, type, onClose, onSaved }) {
  const deposit = type === 'Contribution'
  return (
    <Modal
      open={open}
      title={deposit ? `Add money to ${goal?.name ?? ''}` : `Take money out of ${goal?.name ?? ''}`}
      description={goal ? `${formatPeso(goal.savedAmount)} in it now.` : undefined}
      onClose={onClose}
    >
      {goal && <MoneyForm goal={goal} type={type} onCancel={onClose} onSaved={onSaved} />}
    </Modal>
  )
}

function MoneyForm({ goal, type, onCancel, onSaved }) {
  const toast = useToast()
  const deposit = type === 'Contribution'
  const [form, setForm] = useState({ amount: '', date: todayIso(), note: '', source: 'budget', spent: false, category: '' })
  const [errors, setErrors] = useState({})
  const [busy, setBusy] = useState(false)
  const update = (field) => (e) => {
    setForm((f) => ({ ...f, [field]: e.target.value }))
    setErrors(({ [field]: _cleared, ...rest }) => rest)
  }
  const spent = !deposit && form.spent

  async function handleSubmit(event) {
    event.preventDefault()
    const amountError = validateAmount(form.amount)
    if (amountError) return setErrors({ amount: amountError })
    if (!deposit && Number(form.amount) > goal.savedAmount) {
      return setErrors({ amount: `You can take out at most ${formatPeso(goal.savedAmount)}.` })
    }
    if (spent && !form.category) return setErrors({ category: 'Choose the expense category.' })
    setBusy(true)
    try {
      const amount = Number(form.amount)
      const updated = await api.addSavingsTransaction(goal.id, {
        type,
        amount,
        date: form.date,
        note: form.note.trim() || null,
        fromOutside: deposit && form.source === 'outside',
        recordExpense: spent,
        category: spent ? form.category : null,
      })
      let message
      if (deposit && updated.status === 'Completed') message = `You reached your ${goal.name} goal!`
      else if (deposit && form.source === 'outside') message = `Added ${formatPeso(amount)} to ${goal.name}. Your budget isn’t touched.`
      else if (deposit) message = `Added ${formatPeso(amount)} to ${goal.name}. It comes out of your budget for that date.`
      else if (spent) message = `Took ${formatPeso(amount)} out of ${goal.name} and added it to your ${form.category} expenses.`
      else message = `Took ${formatPeso(amount)} out of ${goal.name}.`
      toast.success(message)
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

      {deposit ? (
        <fieldset className="cover__options cover__options--stacked">
          <legend>Where is this money from?</legend>
          <label className="cover__option">
            <input type="radio" name="money-source" value="budget" checked={form.source === 'budget'} onChange={update('source')} />
            <span className="cover__option-name">From my budget</span>
            <span className="cover__option-room">Comes out of your budget for this date</span>
          </label>
          <label className="cover__option">
            <input type="radio" name="money-source" value="outside" checked={form.source === 'outside'} onChange={update('source')} />
            <span className="cover__option-name">From somewhere else</span>
            <span className="cover__option-room">Money you already had, a gift or allowance. Your budget isn’t touched</span>
          </label>
        </fieldset>
      ) : (
        <>
          <label className="check">
            <input type="checkbox" checked={form.spent} onChange={(e) => setForm((f) => ({ ...f, spent: e.target.checked }))} />
            <span>
              <span className="check__label">I spent it. Add it as an expense</span>
              <span className="check__hint">
                {form.spent
                  ? `It’s paid from ${goal.name}, so it won’t use your budget or category limits.`
                  : 'If you saved it from your budget in this same period, it goes back to that budget.'}
              </span>
            </span>
          </label>
          {form.spent && (
            <FormField label="Expense category" error={errors.category}>
              {(props) => (
                <select {...props} value={form.category} onChange={update('category')}>
                  <option value="">Choose a category</option>
                  {CATEGORIES.map((c) => (
                    <option key={c} value={c}>{c}</option>
                  ))}
                </select>
              )}
            </FormField>
          )}
        </>
      )}

      <FormField label={spent ? 'What was it for? (optional)' : 'Note (optional)'} error={errors.note}>
        {(props) => (
          <input
            {...props}
            type="text"
            maxLength={120}
            placeholder={deposit ? 'e.g. From allowance' : spent ? 'e.g. Project supplies' : 'e.g. Moved to my wallet'}
            value={form.note}
            onChange={update('note')}
          />
        )}
      </FormField>
      <div className="modal__actions">
        <button type="button" className="button button--ghost" onClick={onCancel} disabled={busy}>Cancel</button>
        <button type="submit" className="button button--primary" disabled={busy}>{busy ? 'Saving…' : deposit ? 'Add money' : 'Take out'}</button>
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

const KIND = (t) => (t.type === 'Contribution' ? 'Added' : t.expenseId != null ? 'Spent' : 'Taken out')
const SOURCE = (t) =>
  t.type === 'Contribution' ? (t.fromOutside ? 'Not from your budget' : 'From your budget') : t.expenseId != null ? 'Added as an expense' : null

function History({ goalId }) {
  const { data, error } = useAsync(() => api.getSavingsTransactions(goalId), [goalId])
  if (error) return <div className="form"><ErrorState error={error} /></div>
  if (!data) return <div className="form"><LoadingState label="Loading history…" variant="rows" /></div>
  if (data.length === 0) {
    return <div className="form"><EmptyState compact title="Nothing added yet" message="Money you add or take out appears here." /></div>
  }
  return (
    <div className="table-wrap history">
      <table className="data-table data-table--keep-head">
        <caption className="visually-hidden">Money added and taken out</caption>
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
              <td>
                {KIND(t)}
                {SOURCE(t) && <span className="history__source">{SOURCE(t)}</span>}
              </td>
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
