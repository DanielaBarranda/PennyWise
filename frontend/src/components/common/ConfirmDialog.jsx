import Icon from './Icon.jsx'
import Modal from './Modal.jsx'

export default function ConfirmDialog({ open, title, message, confirmLabel = 'Delete', busy, onConfirm, onCancel }) {
  return (
    <Modal open={open} title={title} onClose={busy ? () => {} : onCancel}>
      <div className="confirm">
        <span className="confirm__icon" aria-hidden="true">
          <Icon name="trash" size={20} />
        </span>
        <p>{message}</p>
      </div>
      <div className="modal__actions">
        {/* Focus starts on Cancel so pressing Enter by accident never deletes anything. */}
        <button type="button" className="button button--ghost" onClick={onCancel} disabled={busy} data-autofocus>
          Cancel
        </button>
        <button type="button" className="button button--danger" onClick={onConfirm} disabled={busy}>
          {busy ? 'Deleting…' : confirmLabel}
        </button>
      </div>
    </Modal>
  )
}
