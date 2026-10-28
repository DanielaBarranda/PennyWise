import { useEffect, useId, useRef } from 'react'
import Icon from './Icon.jsx'

// Built on the native <dialog> element: showModal() traps keyboard focus inside,
// makes the rest of the page inert, and returns focus to the opening button on close.
export default function Modal({ open, title, description, onClose, children, wide = false }) {
  const ref = useRef(null)
  const titleId = useId()
  const descriptionId = useId()

  useEffect(() => {
    const dialog = ref.current
    if (open && !dialog.open) {
      dialog.showModal()
      // Optional: a child marked data-autofocus gets focus instead of the first button.
      dialog.querySelector('[data-autofocus]')?.focus()
    }
    if (!open && dialog.open) dialog.close()
  }, [open])

  return (
    <dialog
      ref={ref}
      className={`modal${wide ? ' modal--wide' : ''}`}
      aria-labelledby={titleId}
      aria-describedby={description ? descriptionId : undefined}
      // Escape key: let React state decide whether the dialog closes.
      onCancel={(event) => {
        event.preventDefault()
        onClose()
      }}
      // A click on the backdrop lands on the <dialog> element itself.
      onClick={(event) => event.target === ref.current && onClose()}
    >
      {open && (
        <div className="modal__panel">
          <header className="modal__header">
            <div>
              <h2 id={titleId}>{title}</h2>
              {description && (
                <p id={descriptionId} className="modal__description">
                  {description}
                </p>
              )}
            </div>
            <button type="button" className="icon-button" onClick={onClose} aria-label="Close dialog">
              <Icon name="close" />
            </button>
          </header>
          {children}
        </div>
      )}
    </dialog>
  )
}
