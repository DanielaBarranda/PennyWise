// PennyWise service worker. The build (see vite.config.js) replaces the two
// placeholders below with this build's version and the list of its files, so
// every deployment installs a fresh copy of the app shell.
//
// What it caches: only the app itself (HTML, scripts, styles, icons, fonts), so
// PennyWise opens instantly and even without a connection.
// What it never caches: anything under /api. Budgets, expenses and profile
// photos always come straight from the server and are never left on the device.

const VERSION = '__PENNYWISE_VERSION__'
const PRECACHE = __PENNYWISE_PRECACHE__
const APP_CACHE = `pennywise-app-${VERSION}`
const FONT_CACHE = 'pennywise-fonts-v1'
const FONT_HOSTS = ['fonts.googleapis.com', 'fonts.gstatic.com']

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(APP_CACHE).then((cache) => cache.addAll(PRECACHE.map((url) => new Request(url, { cache: 'reload' })))),
  )
  // No skipWaiting here: a new version waits until the user chooses "Reload",
  // so an open page never switches code underneath them.
})

self.addEventListener('activate', (event) => {
  event.waitUntil(
    (async () => {
      const names = await caches.keys()
      await Promise.all(names.filter((n) => n.startsWith('pennywise-app-') && n !== APP_CACHE).map((n) => caches.delete(n)))
      await self.clients.claim()
    })(),
  )
})

self.addEventListener('message', (event) => {
  if (event.data?.type === 'SKIP_WAITING') self.skipWaiting()
})

self.addEventListener('fetch', (event) => {
  const { request } = event
  if (request.method !== 'GET') return
  const url = new URL(request.url)

  if (FONT_HOSTS.includes(url.hostname)) {
    event.respondWith(staleWhileRevalidate(request, FONT_CACHE))
    return
  }
  if (url.origin !== self.location.origin) return
  if (url.pathname.startsWith('/api/')) return // always the network, never stored

  if (request.mode === 'navigate') {
    event.respondWith(networkFirstPage(request))
    return
  }
  event.respondWith(cacheFirst(request))
})

// Pages: the newest index.html when online; this version's copy when offline.
async function networkFirstPage(request) {
  try {
    return await fetch(request)
  } catch {
    const cache = await caches.open(APP_CACHE)
    return (await cache.match('/index.html')) ?? Response.error()
  }
}

// Scripts, styles and icons have the version in their name or rarely change.
async function cacheFirst(request) {
  const cache = await caches.open(APP_CACHE)
  const cached = await cache.match(request, { ignoreSearch: true })
  if (cached) return cached
  const response = await fetch(request)
  if (response.ok) cache.put(request, response.clone())
  return response
}

async function staleWhileRevalidate(request, cacheName) {
  const cache = await caches.open(cacheName)
  const cached = await cache.match(request)
  const refresh = fetch(request)
    .then((response) => {
      if (response.ok || response.type === 'opaque') cache.put(request, response.clone())
      return response
    })
    .catch(() => cached)
  return cached ?? refresh
}
