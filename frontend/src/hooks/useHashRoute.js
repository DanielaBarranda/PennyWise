import { useEffect, useState } from 'react'

// A tiny router: "#/dashboard" and "#/expenses". Two pages do not justify a
// routing library, and hash URLs work behind Nginx without extra rewrite rules.
export const ROUTES = ['dashboard', 'expenses']

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
