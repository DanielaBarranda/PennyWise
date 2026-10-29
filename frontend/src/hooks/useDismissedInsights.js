import { useCallback, useState } from 'react'

// Insights the user has hidden, remembered in this browser only (a convenience:
// nothing is lost if storage is unavailable). Each insight key includes its
// month and situation, so a hidden note reappears when things change.
export function useDismissedInsights(userId) {
  const storageKey = `pennywise.dismissedInsights.${userId}`
  const [dismissed, setDismissed] = useState(() => {
    try {
      return new Set(JSON.parse(localStorage.getItem(storageKey) ?? '[]'))
    } catch {
      return new Set()
    }
  })

  const dismiss = useCallback(
    (key) => {
      setDismissed((current) => {
        const next = new Set(current).add(key)
        try {
          // Keep the most recent 200 so the list cannot grow forever.
          localStorage.setItem(storageKey, JSON.stringify([...next].slice(-200)))
        } catch {
          // Not remembered after a reload; still hidden for now.
        }
        return next
      })
    },
    [storageKey],
  )

  return [dismissed, dismiss]
}
