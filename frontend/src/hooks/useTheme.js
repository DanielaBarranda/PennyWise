import { useCallback, useEffect, useState } from 'react'

const KEY = 'pennywise.theme'
const THEME_COLORS = { dark: '#0d0b0d', light: '#171719' }

// Dark is the default look; the choice is remembered in this browser only.
// index.html applies the saved theme before React loads, so there is no flash.
function readTheme() {
  try {
    return localStorage.getItem(KEY) === 'light' ? 'light' : 'dark'
  } catch {
    return 'dark'
  }
}

export function useTheme() {
  const [theme, setTheme] = useState(readTheme)

  useEffect(() => {
    document.documentElement.dataset.theme = theme
    document.querySelector('meta[name="theme-color"]')?.setAttribute('content', THEME_COLORS[theme])
    try {
      localStorage.setItem(KEY, theme)
    } catch {
      // Not remembered; the switch still works for this visit.
    }
  }, [theme])

  const toggle = useCallback(() => setTheme((t) => (t === 'dark' ? 'light' : 'dark')), [])
  return [theme, toggle]
}
