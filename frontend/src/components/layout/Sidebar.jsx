import { useEffect, useRef, useState } from 'react'
import Icon from '../common/Icon.jsx'
import ThemeToggle from './ThemeToggle.jsx'
import UserMenu from './UserMenu.jsx'

const NAV_ITEMS = [
  { route: 'dashboard', label: 'Dashboard', icon: 'dashboard' },
  { route: 'expenses', label: 'Expenses', icon: 'receipt' },
]

// Matches the breakpoint in layout.css where the sidebar becomes a top bar.
const MOBILE_QUERY = '(max-width: 860px)'

function Brand() {
  return (
    <a href="#/dashboard" className="brand" aria-label="PennyWise home">
      <img className="brand__emblem" src="/brand/pennywise-emblem.png" alt="" width="40" height="37" />
      <span className="brand__text">
        <span className="brand__name">PennyWise</span>
        <span className="brand__tagline">Track • Plan • Grow</span>
      </span>
    </a>
  )
}

function NavLinks({ route, onNavigate }) {
  return (
    <ul>
      {NAV_ITEMS.map((item) => (
        <li key={item.route}>
          <a
            href={`#/${item.route}`}
            className="nav__link"
            aria-current={route === item.route ? 'page' : undefined}
            onClick={onNavigate}
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
  )
}

// Desktop: a sidebar that can collapse to an icon rail.
// Tablets and phones: a top bar with a hamburger button that opens the menu in a
// slide-in panel. The panel is a native <dialog>, so focus stays inside it while
// open, Escape closes it, and focus returns to the hamburger button afterwards.
export default function Sidebar({ route, collapsed, theme, onToggleTheme }) {
  const [menuOpen, setMenuOpen] = useState(false)
  const drawerRef = useRef(null)

  useEffect(() => {
    const drawer = drawerRef.current
    if (menuOpen && !drawer.open) drawer.showModal()
    if (!menuOpen && drawer.open) drawer.close()
  }, [menuOpen])

  // Close the panel if the window grows to desktop width while it is open.
  useEffect(() => {
    const media = window.matchMedia(MOBILE_QUERY)
    const onChange = () => !media.matches && setMenuOpen(false)
    media.addEventListener('change', onChange)
    return () => media.removeEventListener('change', onChange)
  }, [])

  return (
    <aside id="sidebar" className={`sidebar${collapsed ? ' sidebar--collapsed' : ''}`}>
      <Brand />

      <nav className="nav" aria-label="Main">
        <p className="nav__heading">Menu</p>
        <NavLinks route={route} />
      </nav>

      <div className="sidebar__mobile-actions">
        <ThemeToggle theme={theme} onToggle={onToggleTheme} className="sidebar__theme" />
        <button
          type="button"
          className="icon-button sidebar__menu-button"
          onClick={() => setMenuOpen(true)}
          aria-controls="mobile-menu"
          aria-expanded={menuOpen}
          aria-label="Open menu"
        >
          <Icon name="menu" size={22} />
        </button>
      </div>

      <dialog
        id="mobile-menu"
        ref={drawerRef}
        className="drawer"
        aria-label="Menu"
        onCancel={(event) => {
          event.preventDefault()
          setMenuOpen(false)
        }}
        // A tap on the dimmed area outside the panel lands on the <dialog> itself.
        onClick={(event) => event.target === drawerRef.current && setMenuOpen(false)}
      >
        <div className="drawer__header">
          <Brand />
          <button type="button" className="icon-button drawer__close" onClick={() => setMenuOpen(false)} aria-label="Close menu">
            <Icon name="close" size={22} />
          </button>
        </div>
        <nav className="drawer__nav" aria-label="Main">
          <NavLinks route={route} onNavigate={() => setMenuOpen(false)} />
        </nav>
        <UserMenu variant="panel" />
      </dialog>
    </aside>
  )
}
