import { useEffect, useState } from 'react'
import Sidebar from './components/layout/Sidebar.jsx'
import Topbar from './components/layout/Topbar.jsx'
import ToastProvider from './components/common/ToastProvider.jsx'
import AuthProvider from './components/auth/AuthProvider.jsx'
import AuthLayout from './components/auth/AuthLayout.jsx'
import DashboardPage from './pages/DashboardPage.jsx'
import ExpensesPage from './pages/ExpensesPage.jsx'
import ProfilePage from './pages/ProfilePage.jsx'
import SavingsPage from './pages/SavingsPage.jsx'
import BillsPage from './pages/BillsPage.jsx'
import InsightsPage from './pages/InsightsPage.jsx'
import ReportsPage from './pages/ReportsPage.jsx'
import LoginPage from './pages/LoginPage.jsx'
import RegisterPage from './pages/RegisterPage.jsx'
import { ForgotPasswordPage, ResetPasswordPage, VerifyEmailPage } from './pages/AccountEmailPages.jsx'
import VerifyEmailBanner from './components/auth/VerifyEmailBanner.jsx'
import OfflineScreen from './components/common/OfflineScreen.jsx'
import PwaNotice from './components/common/PwaNotice.jsx'
import { useAuth } from './hooks/useAuth.js'
import { useHashRoute } from './hooks/useHashRoute.js'
import Tour from './components/tour/Tour.jsx'
import { fullTour, pageTour } from './components/tour/tourSteps.js'
import { useSidebarCollapsed } from './hooks/useSidebarCollapsed.js'
import { useTheme } from './hooks/useTheme.js'
import { periodStart } from './utils/budgetPeriod.js'
import { currentMonth, todayIso } from './utils/dates.js'

const AUTH_ROUTES = ['login', 'register']
// Opened from email links (or "Forgot password?"): shown whether or not someone is signed in.
const EMAIL_PAGES = { 'forgot-password': ForgotPasswordPage, 'reset-password': ResetPasswordPage, 'verify-email': VerifyEmailPage }

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
  const { user, status, retrySession } = useAuth()
  const route = useHashRoute()
  const onAuthPage = AUTH_ROUTES.includes(route)

  useEffect(() => {
    if (status !== 'ready' || EMAIL_PAGES[route]) return
    if (!user && !onAuthPage) window.location.replace('#/login')
    if (user && onAuthPage) window.location.replace('#/dashboard')
  }, [user, status, onAuthPage, route])

  if (status === 'loading') {
    return (
      <div className="splash" role="status">
        <img src="/brand/pennywise-emblem.png" alt="" width="66" height="61" />
        <span className="visually-hidden">Loading PennyWise…</span>
      </div>
    )
  }

  if (status === 'offline') return <OfflineScreen onRetry={retrySession} />

  const EmailPage = EMAIL_PAGES[route]
  if (EmailPage) {
    return (
      <AuthLayout theme={theme} onToggleTheme={onToggleTheme}>
        <EmailPage />
      </AuthLayout>
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
  const { user, setTourCompleted } = useAuth()
  // The full tour (every page) opens by itself until the account has finished
  // or skipped it once. "Tour this page" in the menu tours only the current page;
  // "Take the full tour" on Profile replays the whole thing.
  const [tour, setTour] = useState(() => (user.tourCompleted ? null : { full: true, steps: fullTour() }))
  const endTour = () => {
    // Not being able to save this only means the tour shows again next time.
    if (tour?.full && !user.tourCompleted) setTourCompleted(true).catch(() => {})
    setTour(null)
  }
  // The selected month is shared, so switching pages keeps the same month.
  const [month, setMonth] = useState(currentMonth)
  // The dashboard and insights follow the account's budget period: a month (the
  // shared month above), or the day/week containing `anchor`.
  const [anchor, setAnchor] = useState(todayIso)
  const period = user.budgetPeriod ?? 'Monthly'
  const start = period === 'Monthly' ? `${month}-01` : periodStart(period, anchor)
  const onStartChange = (next) => (period === 'Monthly' ? setMonth(next.slice(0, 7)) : setAnchor(next))
  const [collapsed, toggleSidebar] = useSidebarCollapsed()
  const Page =
    {
      expenses: ExpensesPage,
      savings: SavingsPage,
      bills: BillsPage,
      insights: InsightsPage,
      reports: ReportsPage,
      profile: ProfilePage,
    }[route] ?? DashboardPage

  return (
    <div className={`app-shell${collapsed ? ' app-shell--collapsed' : ''}`}>
      <Sidebar route={route} collapsed={collapsed} theme={theme} onToggleTheme={onToggleTheme} onStartTour={() => setTour({ full: false, steps: pageTour(route) })} />
      <div className="content">
        <Topbar collapsed={collapsed} onToggleSidebar={toggleSidebar} theme={theme} onToggleTheme={onToggleTheme} />
        <main id="main" className="main" tabIndex={-1}>
          <PwaNotice />
          <VerifyEmailBanner />
          <Page
            month={month}
            onMonthChange={setMonth}
            period={period}
            start={start}
            onStartChange={onStartChange}
            onStartFullTour={() => setTour({ full: true, steps: fullTour() })}
          />
        </main>
      </div>
      <Tour steps={tour?.steps ?? null} route={route} userName={user.name} onClose={endTour} />
    </div>
  )
}
