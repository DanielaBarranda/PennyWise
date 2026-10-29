import Icon from '../common/Icon.jsx'
import ThemeToggle from './ThemeToggle.jsx'
import AccountMenu from './AccountMenu.jsx'

const today = () =>
  new Date().toLocaleDateString('en-PH', { weekday: 'long', month: 'long', day: 'numeric', year: 'numeric' })

// Slim bar above the page: sidebar toggle, app description, today's date, the
// light/dark switch and the account menu (Profile, Log out).
export default function Topbar({ collapsed, onToggleSidebar, theme, onToggleTheme }) {
  return (
    <header className="topbar">
      <button
        type="button"
        className="icon-button topbar__toggle"
        onClick={onToggleSidebar}
        aria-controls="sidebar"
        aria-expanded={!collapsed}
        aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
        title={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
      >
        <Icon name="sidebar" size={20} />
      </button>
      <div className="topbar__text">
        <p className="topbar__title">Personal budget tracker</p>
        <p className="topbar__date">{today()}</p>
      </div>
      <ThemeToggle theme={theme} onToggle={onToggleTheme} className="topbar__theme" />
      <AccountMenu />
    </header>
  )
}
