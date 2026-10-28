import { createContext, useContext } from 'react'

export const ToastContext = createContext(null)

// Usage: const toast = useToast(); toast.success('Saved'); toast.error('Failed')
export function useToast() {
  const context = useContext(ToastContext)
  if (!context) throw new Error('useToast must be used inside <ToastProvider>.')
  return context
}
