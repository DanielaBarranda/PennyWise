import Icon from '../common/Icon.jsx'
import ThemeToggle from '../layout/ThemeToggle.jsx'

const POINTS = [
  { icon: 'receipt', text: 'See where every peso goes, month by month' },
  { icon: 'target', text: 'Set a monthly budget and limits per category' },
  { icon: 'alert', text: 'Get warned before you overspend' },
]

// Two panels on wide screens (brand on the left, form on the right); on phones
// the brand shrinks to a header above the form.
export default function AuthLayout({ theme, onToggleTheme, children }) {
  return (
    <div className="auth">
      <aside className="auth__brand">
        <div className="auth__brand-inner">
          <img className="auth__emblem" src="/brand/pennywise-emblem.png" alt="" width="132" height="122" />
          <p className="auth__name">PennyWise</p>
          <p className="auth__tagline">Track • Plan • Grow</p>
          <ul className="auth__points">
            {POINTS.map((p) => (
              <li key={p.text}>
                <span className="auth__point-icon" aria-hidden="true">
                  <Icon name={p.icon} size={18} />
                </span>
                {p.text}
              </li>
            ))}
          </ul>
        </div>
      </aside>

      <main id="main" className="auth__main" tabIndex={-1}>
        <div className="auth__toolbar">
          <ThemeToggle theme={theme} onToggle={onToggleTheme} />
        </div>
        <div className="auth__card">{children}</div>
      </main>
    </div>
  )
}
