import { useCallback, useState } from 'react'

const KEY = 'pennywise.sidebarCollapsed'

// Remembers whether the sidebar is collapsed, in this browser only. Storage can
// be unavailable (private windows, blocked site data), so every access is guarded
// and the app simply starts expanded when it fails.
export function useSidebarCollapsed() {
  const [collapsed, setCollapsed] = useState(() => {
    try {
      return localStorage.getItem(KEY) === 'true'
    } catch {
      return false
    }
  })

  const toggle = useCallback(() => {
    setCollapsed((value) => {
      try {
        localStorage.setItem(KEY, String(!value))
      } catch {
        // Not remembered; the toggle still works for this visit.
      }
      return !value
    })
  }, [])

  return [collapsed, toggle]
}
