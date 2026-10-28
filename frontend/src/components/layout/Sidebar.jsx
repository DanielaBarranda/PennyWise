import Icon from '../common/Icon.jsx'

const NAV_ITEMS = [
  { route: 'dashboard', label: 'Dashboard', icon: 'dashboard' },
  { route: 'expenses', label: 'Expenses', icon: 'receipt' },
]

// Expanded: emblem, name and tagline, then labelled links.
// Collapsed: a narrow icon rail; each link keeps its text for screen readers
// and shows it as a tooltip on hover or keyboard focus.
export default function Sidebar({ route, collapsed }) {
  return (
    <aside id="sidebar" className={`sidebar${collapsed ? ' sidebar--collapsed' : ''}`}>
      <a href="#/dashboard" className="brand" aria-label="PennyWise home">
        <img className="brand__emblem" src="/brand/pennywise-emblem.png" alt="" width="40" height="37" />
        <span className="brand__text">
          <span className="brand__name">PennyWise</span>
          <span className="brand__tagline">Track • Plan • Grow</span>
        </span>
      </a>

      <nav className="nav" aria-label="Main">
        <p className="nav__heading">Menu</p>
        <ul>
          {NAV_ITEMS.map((item) => (
            <li key={item.route}>
              <a
                href={`#/${item.route}`}
                className="nav__link"
                aria-current={route === item.route ? 'page' : undefined}
              >
                <Icon name={item.icon} size={20} />
                <span className="nav__label">{item.label}</span>
                {/* Visual-only copy of the label for the collapsed rail; hidden from
                    screen readers so the link name is not read twice. */}
                <span className="nav__tooltip" aria-hidden="true">
                  {item.label}
                </span>
              </a>
            </li>
          ))}
        </ul>
      </nav>
    </aside>
  )
}
