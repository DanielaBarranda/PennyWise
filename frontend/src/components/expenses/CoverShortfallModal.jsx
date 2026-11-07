import { useState } from 'react'
import Modal from '../common/Modal.jsx'
import Icon from '../common/Icon.jsx'
import { api } from '../../services/api.js'
import { useToast } from '../../hooks/useToast.js'
import { formatPeso } from '../../utils/format.js'
import { UNASSIGNED, moveIntoLimit } from '../../utils/shortfall.js'

// Shown right after an expense pushes a category over its limit: "Oops, that's
// over" plus a choice of where to take the extra from. Moving only changes the
// category limits; the budget's total stays the same.
// `cover` is null when closed, or { shortfall, budget, label } (see utils/shortfall.js).
export default function CoverShortfallModal({ cover, onClose, onMoved }) {
  const s = cover?.shortfall
  return (
    <Modal
      open={cover !== null}
      title={s ? `Oops! ${s.category} is over its limit` : ''}
      description={s ? `You’ve spent ${formatPeso(s.spent)} of the ${formatPeso(s.limit)} ${s.category} limit in your ${cover.label}: ${formatPeso(s.over)} over.` : undefined}
      onClose={onClose}
    >
      {cover && <CoverForm cover={cover} onClose={onClose} onMoved={onMoved} />}
    </Modal>
  )
}

function CoverForm({ cover, onClose, onMoved }) {
  const { shortfall: s, budget } = cover
  const toast = useToast()
  const [source, setSource] = useState(s.sources[0]?.key ?? null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState(null)
  const chosen = s.sources.find((o) => o.key === source)
  const amount = chosen ? Math.min(s.over, chosen.available) : 0

  async function handleSubmit(event) {
    event.preventDefault()
    if (!chosen) return
    const { limits, moved } = moveIntoLimit(budget, s, source)
    setBusy(true)
    setError(null)
    try {
      await api.updateBudget(budget.id, { period: budget.period, start: budget.start, amount: budget.amount, categoryLimits: limits })
      const from = source === UNASSIGNED ? 'money not given to a category' : source
      toast.success(`Moved ${formatPeso(moved)} from ${from} to ${s.category}.`)
      onMoved()
    } catch (err) {
      setError(err.message)
      setBusy(false)
    }
  }

  return (
    <form className="form" onSubmit={handleSubmit} noValidate>
      {error && (
        <p className="form__error" role="alert">
          <Icon name="alert" size={18} />
          {error}
        </p>
      )}
      {s.budgetOver > 0 && (
        <p className="cover__warning">
          <Icon name="alert" size={16} />
          You’re also {formatPeso(s.budgetOver)} over the whole budget, so moving limits won’t give you more money to spend.
        </p>
      )}

      {s.sources.length > 0 ? (
        <fieldset className="cover__options">
          <legend>Where should the {formatPeso(s.over)} come from?</legend>
          {s.sources.map((o) => (
            <label key={o.key} className="cover__option">
              <input type="radio" name="cover-source" value={o.key} checked={source === o.key} onChange={() => setSource(o.key)} />
              <span className="cover__option-name">{o.label}</span>
              <span className="cover__option-room">
                {formatPeso(o.available)} available{o.available < s.over ? ` (covers ${formatPeso(o.available)})` : ''}
              </span>
            </label>
          ))}
          <p className="field__hint">
            {source === UNASSIGNED
              ? `This raises the ${s.category} limit with budget no category was using.`
              : `This lowers the ${source} limit and raises the ${s.category} limit.`}{' '}
            Your total budget stays the same.
          </p>
        </fieldset>
      ) : (
        <p className="cover__empty">No other category has money left to move. You can raise the budget with Edit budget on the dashboard.</p>
      )}

      <div className="modal__actions">
        <button type="button" className="button button--ghost" onClick={onClose} disabled={busy}>
          {s.sources.length > 0 ? 'Not now' : 'OK'}
        </button>
        {s.sources.length > 0 && (
          <button type="submit" className="button button--primary" disabled={busy || !chosen} data-autofocus>
            {busy ? 'Moving…' : `Move ${formatPeso(amount)}`}
          </button>
        )}
      </div>
    </form>
  )
}
