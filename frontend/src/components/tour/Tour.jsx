import { useCallback, useEffect, useId, useRef, useState } from 'react'
import Icon from '../common/Icon.jsx'

// The app tour: a short walk through the Dashboard. Each step dims the page and
// highlights one real part of it (found by its data-tour="…" marker), with a
// card explaining it. A step whose part is not on screen (e.g. the sidebar on a
// phone) shows its card in the middle instead.
//
// Built on a full-screen native <dialog>: keyboard focus stays in the card,
// Escape skips the tour, and focus returns to where it was afterwards.
const STEPS = [
  {
    title: (name) => `Welcome to PennyWise, ${name}`,
    body: 'This quick tour shows you around. It takes about a minute, and you can skip it at any time.',
  },
  {
    targets: ['period'],
    title: 'Choose how you budget',
    body: 'Budget by the day, the week or the month. Each period has its own budget, and the arrows move between them.',
  },
  {
    targets: ['budget'],
    title: 'Set your budget',
    body: 'Set how much you plan to spend in this period, plus optional limits for categories like Food or School.',
  },
  {
    targets: ['add-expense'],
    title: 'Log what you spend',
    body: 'Add each expense as it happens. It counts against your budget right away, and PennyWise warns you before you overspend.',
  },
  {
    targets: ['left-to-spend'],
    title: 'See what’s left',
    body: 'How much you can still spend in this period, after spending and unpaid bills. Switch to Per day to see a daily amount.',
  },
  {
    targets: ['nav', 'menu-button'],
    title: 'Everything else is here',
    body: 'Expenses lists everything you’ve logged. Savings tracks your goals, Bills reminds you what’s due, Insights points out patterns, and Reports gives a monthly summary you can download.',
  },
  {
    targets: ['account'],
    title: 'Your profile',
    body: 'Change your photo, your budget period, and turn on two-step verification to keep your account safe.',
  },
  {
    targets: ['tour-button', 'menu-button'],
    title: 'See this again anytime',
    body: 'Use Take the tour at the bottom of the menu whenever you want a refresher.',
  },
]

const GAP = 12 // space between the highlighted part and the card
const PAD = 6 // highlight padding around the part

// The first marked element that is actually visible.
function findTarget(names = []) {
  for (const name of names) {
    for (const el of document.querySelectorAll(`[data-tour="${name}"]`)) {
      const r = el.getBoundingClientRect()
      if (r.width > 0 && r.height > 0 && getComputedStyle(el).visibility !== 'hidden') return el
    }
  }
  return null
}

export default function Tour({ open, userName, onClose }) {
  const dialogRef = useRef(null)
  const cardRef = useRef(null)
  const nextRef = useRef(null)
  const titleId = useId()
  const bodyId = useId()
  const [index, setIndex] = useState(0)
  // { index, hole, card } in viewport pixels; ignored once the step has moved on.
  const [placed, setLayout] = useState(null)
  const layout = placed?.index === index ? placed : null
  const step = STEPS[index]
  const last = index === STEPS.length - 1

  // Finishing or skipping; the next opening starts from the welcome again.
  const finish = () => {
    setIndex(0)
    onClose()
  }

  useEffect(() => {
    const dialog = dialogRef.current
    if (open && !dialog.open) dialog.showModal()
    if (!open && dialog.open) dialog.close()
  }, [open])

  // Where the highlight and the card go for this step.
  const place = useCallback(() => {
    const card = cardRef.current
    if (!card) return
    const target = findTarget(step.targets)
    const vw = window.innerWidth
    const vh = window.innerHeight
    const cw = card.offsetWidth
    const ch = card.offsetHeight
    if (!target) {
      setLayout({ index, hole: null, card: { left: (vw - cw) / 2, top: Math.max(16, (vh - ch) / 2) } })
      return
    }
    const r = target.getBoundingClientRect()
    const hole = { left: r.left - PAD, top: r.top - PAD, width: r.width + PAD * 2, height: r.height + PAD * 2 }
    let top
    let left
    if (vw >= 700 && r.right + GAP + cw <= vw - 16 && r.height > ch * 0.8) {
      // Tall parts (the sidebar): card beside them.
      left = r.right + PAD + GAP
      top = Math.min(Math.max(16, r.top), vh - ch - 16)
    } else {
      // Below when it fits, otherwise above; kept inside the screen.
      top = hole.top + hole.height + GAP + ch <= vh - 16 ? hole.top + hole.height + GAP : hole.top - GAP - ch
      top = Math.min(Math.max(16, top), vh - ch - 16)
      left = Math.min(Math.max(16, r.left + r.width / 2 - cw / 2), vw - cw - 16)
    }
    setLayout({ index, hole, card: { left, top } })
  }, [step, index])

  // On each step: bring the part into view, then place everything and move focus.
  useEffect(() => {
    if (!open) return
    let cancelled = false
    let tries = 0
    // The Dashboard may still be loading its cards; wait briefly for the part.
    const settle = () => {
      if (cancelled) return
      const target = findTarget(step.targets)
      if (!target && step.targets && tries++ < 15) return setTimeout(settle, 100)
      const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches
      target?.scrollIntoView({ block: 'center', inline: 'nearest', behavior: reduce ? 'auto' : 'smooth' })
      setTimeout(() => {
        if (cancelled) return
        place()
        nextRef.current?.focus()
      }, reduce ? 0 : 350)
    }
    settle()
    return () => {
      cancelled = true
    }
  }, [open, step, place])

  useEffect(() => {
    if (!open) return
    window.addEventListener('resize', place)
    window.addEventListener('scroll', place, true)
    return () => {
      window.removeEventListener('resize', place)
      window.removeEventListener('scroll', place, true)
    }
  }, [open, place])

  // Keyboard focus follows the tour: always on the main button of the card.
  useEffect(() => {
    if (open) nextRef.current?.focus({ preventScroll: true })
  }, [open, index, layout])

  const next = () => (last ? finish() : setIndex((i) => i + 1))
  const back = () => setIndex((i) => Math.max(0, i - 1))

  function handleKeyDown(event) {
    if (event.key === 'ArrowRight') next()
    if (event.key === 'ArrowLeft') back()
  }

  const firstName = userName?.split(' ')[0] ?? ''

  return (
    <dialog
      ref={dialogRef}
      className="tour"
      aria-labelledby={titleId}
      aria-describedby={bodyId}
      onCancel={(event) => {
        event.preventDefault()
        finish()
      }}
      onKeyDown={handleKeyDown}
    >
      {open && (
        <>
          <div
            className={`tour__hole${layout?.hole ? '' : ' tour__hole--none'}`}
            style={layout?.hole ?? undefined}
            aria-hidden="true"
          />
          <div
            ref={cardRef}
            className="tour__card"
            style={layout ? { left: layout.card.left, top: layout.card.top } : { visibility: 'hidden' }}
          >
            <p className="tour__count" aria-live="polite">
              {index === 0 ? 'App tour' : `${index} of ${STEPS.length - 1}`}
            </p>
            <h2 id={titleId} className="tour__title">
              {typeof step.title === 'function' ? step.title(firstName) : step.title}
            </h2>
            <p id={bodyId} className="tour__body">{step.body}</p>
            <div className="tour__actions">
              {!last && (
                <button type="button" className="button button--link tour__skip" onClick={finish}>
                  {index === 0 ? 'Skip tour' : 'Skip'}
                </button>
              )}
              {index > 0 && (
                <button type="button" className="button button--ghost button--small" onClick={back}>
                  <Icon name="chevronLeft" size={16} />
                  Back
                </button>
              )}
              <button ref={nextRef} type="button" className="button button--primary button--small" onClick={next}>
                {index === 0 ? 'Start tour' : last ? 'Done' : 'Next'}
                {!last && <Icon name="chevronRight" size={16} />}
              </button>
            </div>
          </div>
        </>
      )}
    </dialog>
  )
}
