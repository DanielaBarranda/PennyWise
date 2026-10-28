import Icon from '../common/Icon.jsx'

const NAV_ITEMS = [
  { route: 'dashboard', label: 'Dashboard', icon: 'dashboard' },
  { route: 'expenses', label: 'Expenses', icon: 'receipt' },
]

export default function Sidebar({ route }) {
  return (
    <aside className="sidebar">
      <a href="#/dashboard" className="brand" aria-label="PennyWise home">
        {/* Full logo on wide screens; the emblem plus a text wordmark on small ones. */}
        <img className="brand__logo" src="/brand/pennywise-logo.png" alt="PennyWise. Track, plan, grow." width="180" height="193" />
        <img className="brand__emblem" src="/brand/pennywise-emblem.png" alt="" width="44" height="41" />
        <span className="brand__wordmark">PennyWise</span>
      </a>

      <nav className="nav" aria-label="Main">
        <ul>
          {NAV_ITEMS.map((item) => (
            <li key={item.route}>
              <a
                href={`#/${item.route}`}
                className="nav__link"
                aria-current={route === item.route ? 'page' : undefined}
              >
                <Icon name={item.icon} size={20} />
                {item.label}
              </a>
            </li>
          ))}
        </ul>
      </nav>
    </aside>
  )
}
