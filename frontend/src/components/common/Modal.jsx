import { useEffect, useId, useRef } from 'react'

// Built on the native <dialog> element: showModal() traps keyboard focus inside,
// makes the rest of the page inert, and returns focus to the opening button on close.
export default function Modal({ open, title, onClose, children, wide = false }) {
  const ref = useRef(null)
  const titleId = useId()

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
            <h2 id={titleId}>{title}</h2>
            <button type="button" className="icon-button" onClick={onClose} aria-label="Close dialog">
              ×
            </button>
          </header>
          {children}
        </div>
      )}
    </dialog>
  )
}
