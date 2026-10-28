import Icon from '../common/Icon.jsx'

const today = () =>
  new Date().toLocaleDateString('en-PH', { weekday: 'long', month: 'long', day: 'numeric', year: 'numeric' })

// Slim bar above the page: the sidebar toggle, plus the app name and today's date.
export default function Topbar({ collapsed, onToggleSidebar }) {
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
    </header>
  )
}
