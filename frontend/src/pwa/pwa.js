import { useSyncExternalStore } from 'react'

// Everything the app needs to know about running as an installed app (PWA):
// whether a new version is waiting, whether the browser offers "Install", and
// whether there is a connection. Components read it through the hooks below.

let state = {
  updateReady: false,
  canInstall: false,
  online: typeof navigator === 'undefined' ? true : navigator.onLine,
}
let waitingWorker = null
let installEvent = null
const listeners = new Set()

function set(patch) {
  state = { ...state, ...patch }
  listeners.forEach((listener) => listener())
}

const subscribe = (listener) => {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

export function usePwa() {
  return useSyncExternalStore(subscribe, () => state)
}

// Running from the home screen / app list rather than a browser tab.
export const isStandalone = () =>
  window.matchMedia('(display-mode: standalone)').matches || window.navigator.standalone === true

// iPhone and iPad Safari never offer an install prompt; people add the app from the Share menu.
export const isIos = () =>
  /iphone|ipad|ipod/i.test(navigator.userAgent) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1)

// Called once from main.jsx, before React renders, so no browser event is missed.
export function startPwa() {
  window.addEventListener('online', () => set({ online: true }))
  window.addEventListener('offline', () => set({ online: false }))

  window.addEventListener('beforeinstallprompt', (event) => {
    event.preventDefault() // show our own "Install app" button instead of the browser's banner
    installEvent = event
    set({ canInstall: true })
  })
  window.addEventListener('appinstalled', () => {
    installEvent = null
    set({ canInstall: false })
  })

  // Only the production build has a service worker (see vite.config.js).
  if (!import.meta.env.PROD || !('serviceWorker' in navigator)) return
  window.addEventListener('load', () => registerServiceWorker())
}

async function registerServiceWorker() {
  let registration
  try {
    registration = await navigator.serviceWorker.register('/sw.js', { scope: '/' })
  } catch {
    return // the app works normally without it, just not offline
  }

  const offerUpdate = (worker) => {
    waitingWorker = worker
    set({ updateReady: true })
  }
  // A new version only counts as an update when an older one is already in control.
  if (registration.waiting && navigator.serviceWorker.controller) offerUpdate(registration.waiting)
  registration.addEventListener('updatefound', () => {
    const worker = registration.installing
    worker?.addEventListener('statechange', () => {
      if (worker.state === 'installed' && navigator.serviceWorker.controller) offerUpdate(worker)
    })
  })

  // When the new version takes over, reload once so the page runs its code.
  let reloading = false
  navigator.serviceWorker.addEventListener('controllerchange', () => {
    if (reloading || !waitingWorker) return
    reloading = true
    window.location.reload()
  })

  // Installed apps can stay open for days: look for new versions now and then.
  const check = () => registration.update().catch(() => {})
  setInterval(check, 60 * 60 * 1000)
  document.addEventListener('visibilitychange', () => document.visibilityState === 'visible' && check())
}

export function applyUpdate() {
  if (waitingWorker) waitingWorker.postMessage({ type: 'SKIP_WAITING' })
  else window.location.reload()
}

export async function promptInstall() {
  if (!installEvent) return false
  installEvent.prompt()
  const { outcome } = await installEvent.userChoice
  installEvent = null
  set({ canInstall: false })
  return outcome === 'accepted'
}
