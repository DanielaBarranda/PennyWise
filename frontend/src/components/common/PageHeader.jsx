import { useEffect, useRef } from 'react'

// Moves keyboard and screen-reader focus to the page heading when the page
// changes, so users hear where they are instead of staying on the old link.
export default function PageHeader({ title, subtitle, children }) {
  const headingRef = useRef(null)

  useEffect(() => {
    document.title = `${title} · PennyWise`
    headingRef.current?.focus()
  }, [title])

  return (
    <div className="page-header">
      <div>
        <h1 ref={headingRef} tabIndex={-1}>
          {title}
        </h1>
        {subtitle && <p className="page-header__subtitle">{subtitle}</p>}
      </div>
      <div className="page-header__actions">{children}</div>
    </div>
  )
}
