import { useEffect, useState } from 'react'
import Sidebar from './components/layout/Sidebar.jsx'
import Topbar from './components/layout/Topbar.jsx'
import ToastProvider from './components/common/ToastProvider.jsx'
import AuthProvider from './components/auth/AuthProvider.jsx'
import AuthLayout from './components/auth/AuthLayout.jsx'
import DashboardPage from './pages/DashboardPage.jsx'
import ExpensesPage from './pages/ExpensesPage.jsx'
import ProfilePage from './pages/ProfilePage.jsx'
import LoginPage from './pages/LoginPage.jsx'
import RegisterPage from './pages/RegisterPage.jsx'
import { useAuth } from './hooks/useAuth.js'
import { useHashRoute } from './hooks/useHashRoute.js'
import { useSidebarCollapsed } from './hooks/useSidebarCollapsed.js'
import { useTheme } from './hooks/useTheme.js'
import { currentMonth } from './utils/dates.js'

const AUTH_ROUTES = ['login', 'register']

export default function App() {
  const [theme, toggleTheme] = useTheme()

  return (
    <ToastProvider>
      <a
        href="#main"
        className="skip-link"
        onClick={(e) => {
          e.preventDefault()
          document.getElementById('main')?.focus()
        }}
      >
        Skip to main content
      </a>
      <AuthProvider>
        <Routes theme={theme} onToggleTheme={toggleTheme} />
      </AuthProvider>
    </ToastProvider>
  )
}

// Signed out: only the login and register pages. Signed in: the app, and the
// login/register pages send you to the dashboard instead.
function Routes({ theme, onToggleTheme }) {
  const { user, status } = useAuth()
  const route = useHashRoute()
  const onAuthPage = AUTH_ROUTES.includes(route)

  useEffect(() => {
    if (status !== 'ready') return
    if (!user && !onAuthPage) window.location.replace('#/login')
    if (user && onAuthPage) window.location.replace('#/dashboard')
  }, [user, status, onAuthPage])

  if (status === 'loading') {
    return (
      <div className="splash" role="status">
        <img src="/brand/pennywise-emblem.png" alt="" width="66" height="61" />
        <span className="visually-hidden">Loading PennyWise…</span>
      </div>
    )
  }

  if (!user) {
    return (
      <AuthLayout theme={theme} onToggleTheme={onToggleTheme}>
        {route === 'register' ? <RegisterPage /> : <LoginPage />}
      </AuthLayout>
    )
  }

  // key: a different account never sees the previous one's selected month or open dialogs.
  return <Workspace key={user.id} route={route} theme={theme} onToggleTheme={onToggleTheme} />
}

function Workspace({ route, theme, onToggleTheme }) {
  // The selected month is shared, so switching pages keeps the same month.
  const [month, setMonth] = useState(currentMonth)
  const [collapsed, toggleSidebar] = useSidebarCollapsed()
  const Page = { expenses: ExpensesPage, profile: ProfilePage }[route] ?? DashboardPage

  return (
    <div className={`app-shell${collapsed ? ' app-shell--collapsed' : ''}`}>
      <Sidebar route={route} collapsed={collapsed} theme={theme} onToggleTheme={onToggleTheme} />
      <div className="content">
        <Topbar collapsed={collapsed} onToggleSidebar={toggleSidebar} theme={theme} onToggleTheme={onToggleTheme} />
        <main id="main" className="main" tabIndex={-1}>
          <Page month={month} onMonthChange={setMonth} />
        </main>
      </div>
    </div>
  )
}
