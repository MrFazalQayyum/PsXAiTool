// PSX Intelligence Service Worker — Web Push handler
self.addEventListener("push", (event) => {
  if (!event.data) return;
  let payload;
  try { payload = event.data.json(); } catch { payload = { title: "PSX Signal", body: event.data.text() }; }

  const options = {
    body: payload.body || "",
    icon: "/favicon.ico",
    badge: "/favicon.ico",
    tag: payload.signal_type || "psx-signal",
    data: { url: payload.url || "/signals" },
    vibrate: [200, 100, 200],
    actions: [{ action: "view", title: "View Signals" }],
  };

  event.waitUntil(self.registration.showNotification(payload.title || "PSX Intelligence", options));
});

self.addEventListener("notificationclick", (event) => {
  event.notification.close();
  const url = event.notification.data?.url || "/signals";
  event.waitUntil(clients.matchAll({ type: "window" }).then((wins) => {
    const match = wins.find((w) => w.url.includes(url) && "focus" in w);
    if (match) return match.focus();
    return clients.openWindow(url);
  }));
});
