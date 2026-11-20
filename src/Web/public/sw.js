// Hand-written service worker: the app shell works offline; API calls and the hub always go to the network
// (offline writes are the outbox's job, not the cache's).
const CACHE = 'listmate-shell-v1'

self.addEventListener('install', (event) => {
  event.waitUntil(caches.open(CACHE).then((cache) => cache.addAll(['/', '/manifest.webmanifest', '/icon.svg'])))
  self.skipWaiting()
})

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then((keys) => Promise.all(keys.filter((k) => k !== CACHE).map((k) => caches.delete(k)))),
  )
  self.clients.claim()
})

self.addEventListener('fetch', (event) => {
  const url = new URL(event.request.url)
  if (event.request.method !== 'GET' || url.origin !== location.origin) return
  if (url.pathname.startsWith('/api/') || url.pathname.startsWith('/hubs/') || url.pathname.startsWith('/health')) return

  if (event.request.mode === 'navigate') {
    // Network first for the page, so a deploy shows up; the cached shell when offline.
    event.respondWith(fetch(event.request).catch(() => caches.match('/')))
    return
  }

  // Hashed build assets never change: cache first, fill on miss.
  event.respondWith(
    caches.match(event.request).then(
      (hit) =>
        hit ??
        fetch(event.request).then((response) => {
          const copy = response.clone()
          if (response.ok) void caches.open(CACHE).then((cache) => cache.put(event.request, copy))
          return response
        }),
    ),
  )
})
