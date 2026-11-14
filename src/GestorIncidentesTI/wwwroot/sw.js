// Service worker del Gestor de incidentes TI (PWA).
// - Guarda solo la "cáscara": estilos, scripts, íconos y la página sin conexión. NUNCA guarda
//   páginas con datos (son HTML del servidor con la sesión), la API ni respuestas de formularios.
// - Navegación siempre por red; sin red, la página "Sin conexión".
// - Sin push, sin sincronización en segundo plano y sin consultas periódicas.
const VERSION = "gestorti-v1";
const CASCARA = ["/offline.html", "/js/offline.js", "/js/tema-inicial.js", "/js/tema.js", "/js/efectos.js", "/css/site.css", "/css/efectos.css", "/css/estados.css", "/iconos/icono.svg", "/iconos/icono-192.png", "/manifest.webmanifest"];

self.addEventListener("install", (event) => {
  event.waitUntil(caches.open(VERSION).then((cache) => cache.addAll(CASCARA)).then(() => self.skipWaiting()));
});

self.addEventListener("activate", (event) => {
  event.waitUntil(
    caches.keys()
      .then((nombres) => Promise.all(nombres.filter((n) => n !== VERSION).map((n) => caches.delete(n))))
      .then(() => self.clients.claim()),
  );
});

self.addEventListener("fetch", (event) => {
  const { request } = event;
  if (request.method !== "GET") return;
  const url = new URL(request.url);
  if (url.origin !== self.location.origin) return;

  if (request.mode === "navigate") {
    event.respondWith(fetch(request).catch(() => caches.match("/offline.html")));
    return;
  }
  // Estilos, scripts e íconos: primero la red (así se ven los cambios); la copia, sin conexión.
  if (/^\/(css|js|iconos)\//.test(url.pathname) || url.pathname === "/manifest.webmanifest") {
    event.respondWith(
      fetch(request)
        .then((respuesta) => {
          if (respuesta.ok) {
            const copia = respuesta.clone();
            caches.open(VERSION).then((cache) => cache.put(request, copia));
          }
          return respuesta;
        })
        .catch(() => caches.match(request)),
    );
  }
});
