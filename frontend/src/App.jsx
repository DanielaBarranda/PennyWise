import { useState } from 'react'
import Sidebar from './components/layout/Sidebar.jsx'
import Topbar from './components/layout/Topbar.jsx'
import ToastProvider from './components/common/ToastProvider.jsx'
import DashboardPage from './pages/DashboardPage.jsx'
import ExpensesPage from './pages/ExpensesPage.jsx'
import { useHashRoute } from './hooks/useHashRoute.js'
import { useSidebarCollapsed } from './hooks/useSidebarCollapsed.js'
import { useTheme } from './hooks/useTheme.js'
import { currentMonth } from './utils/dates.js'

export default function App() {
  const route = useHashRoute()
  // The selected month is shared, so switching pages keeps the same month.
  const [month, setMonth] = useState(currentMonth)
  const [collapsed, toggleSidebar] = useSidebarCollapsed()
  const [theme, toggleTheme] = useTheme()
  const Page = route === 'expenses' ? ExpensesPage : DashboardPage

  return (
    <ToastProvider>
      <a href="#main" className="skip-link" onClick={(e) => {
        e.preventDefault()
        document.getElementById('main')?.focus()
      }}>
        Skip to main content
      </a>
      <div className={`app-shell${collapsed ? ' app-shell--collapsed' : ''}`}>
        <Sidebar route={route} collapsed={collapsed} theme={theme} onToggleTheme={toggleTheme} />
        <div className="content">
          <Topbar collapsed={collapsed} onToggleSidebar={toggleSidebar} theme={theme} onToggleTheme={toggleTheme} />
          <main id="main" className="main" tabIndex={-1}>
            <Page month={month} onMonthChange={setMonth} />
          </main>
        </div>
      </div>
    </ToastProvider>
  )
}
