const NAV_ITEMS = [
  { route: 'dashboard', label: 'Dashboard', icon: 'M3 13h8V3H3zm10 8h8V11h-8zM3 21h8v-6H3zm10-18v6h8V3z' },
  { route: 'expenses', label: 'Expenses', icon: 'M4 5h16v2H4zm0 6h16v2H4zm0 6h10v2H4z' },
]

export function BrandMark() {
  return (
    <svg className="brand__mark" viewBox="0 0 64 64" aria-hidden="true">
      <circle cx="32" cy="32" r="30" fill="var(--color-gold)" />
      <circle cx="32" cy="32" r="24" fill="none" stroke="var(--color-primary-dark)" strokeWidth="3" />
      <path
        d="M25 46V18h9.5c5.8 0 9.5 3.4 9.5 8.6S40.3 35 34.5 35H30v11z M30 30.6h4.2c2.9 0 4.6-1.5 4.6-4s-1.7-4-4.6-4H30z"
        fill="var(--color-primary-dark)"
      />
    </svg>
  )
}

export default function Sidebar({ route }) {
  return (
    <aside className="sidebar">
      <a href="#/dashboard" className="brand">
        <BrandMark />
        <span className="brand__text">
          <span className="brand__name">PennyWise</span>
          <span className="brand__tagline">Track • Plan • Grow</span>
        </span>
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
                <svg viewBox="0 0 24 24" className="nav__icon" aria-hidden="true">
                  <path d={item.icon} fill="currentColor" />
                </svg>
                {item.label}
              </a>
            </li>
          ))}
        </ul>
      </nav>
    </aside>
  )
}
