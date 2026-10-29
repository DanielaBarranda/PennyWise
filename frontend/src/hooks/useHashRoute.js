import { useEffect, useState } from 'react'

// A tiny router: "#/dashboard", "#/expenses", "#/profile", "#/login", "#/register". A few pages
// do not justify a routing library, and hash URLs work behind Nginx without
// extra rewrite rules. Which pages a visitor may see is decided in App.jsx.
export const ROUTES = ['dashboard', 'expenses', 'savings', 'bills', 'insights', 'reports', 'profile', 'login', 'register']

function readRoute() {
  const route = window.location.hash.replace(/^#\/?/, '')
  return ROUTES.includes(route) ? route : 'dashboard'
}

export function useHashRoute() {
  const [route, setRoute] = useState(readRoute)

  useEffect(() => {
    const onChange = () => setRoute(readRoute())
    window.addEventListener('hashchange', onChange)
    return () => window.removeEventListener('hashchange', onChange)
  }, [])

  return route
}
