// Michu Homes service worker.
// Network-first: always try the server so pages, styles and the theme stay current,
// and fall back to the cache only when offline. Bump CACHE_NAME to evict old caches.
const CACHE_NAME = 'michuhomes-v2';
const PRECACHE = [
    '/favicon.ico',
    '/images/brand/favicon.svg',
    '/icons/icon-192.png',
    '/icons/icon-512.png'
];

self.addEventListener('install', event => {
    event.waitUntil(caches.open(CACHE_NAME).then(cache => cache.addAll(PRECACHE)));
    self.skipWaiting();
});

self.addEventListener('activate', event => {
    event.waitUntil(
        caches.keys()
            .then(keys => Promise.all(keys.filter(key => key !== CACHE_NAME).map(key => caches.delete(key))))
            .then(() => self.clients.claim())
    );
});

self.addEventListener('fetch', event => {
    const request = event.request;
    // Only static assets are cached; pages (which can contain signed-in data) always go to the network.
    const cacheable = ['style', 'script', 'image', 'font'].includes(request.destination);
    if (request.method !== 'GET' || !cacheable || new URL(request.url).origin !== self.location.origin) {
        return;
    }

    event.respondWith(
        fetch(request)
            .then(response => {
                if (response.ok && response.type === 'basic') {
                    const copy = response.clone();
                    caches.open(CACHE_NAME).then(cache => cache.put(request, copy));
                }
                return response;
            })
            .catch(() => caches.match(request))
    );
});
