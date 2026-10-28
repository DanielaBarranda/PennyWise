import { useEffect, useRef } from 'react'

// Moves keyboard and screen-reader focus to the page heading when the page
// changes, so users hear where they are instead of staying on the old link.
// `controls` (e.g. the month picker) sits apart from the action buttons so
// navigating the data and changing it never look alike.
export default function PageHeader({ title, subtitle, controls, children }) {
  const headingRef = useRef(null)

  useEffect(() => {
    document.title = `${title} · PennyWise`
    headingRef.current?.focus()
  }, [title])

  return (
    <div className="page-header">
      <div className="page-header__text">
        <h1 ref={headingRef} tabIndex={-1}>
          {title}
        </h1>
        {subtitle && <p className="page-header__subtitle">{subtitle}</p>}
      </div>
      <div className="page-header__tools">
        {controls && <div className="page-header__controls">{controls}</div>}
        {children && <div className="page-header__actions">{children}</div>}
      </div>
    </div>
  )
}
