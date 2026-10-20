// Only catalog regions opt in. Checkout reviews and existing payment sessions never do.
(() => {
    const regions = [...document.querySelectorAll("[data-plan-catalog]")];
    if (!regions.length) return;
    let inFlight = false;
    let dirty = false;
    let stopped = false;
    let events;
    const snapshots = new WeakMap();
    const refresh = async () => {
        if (stopped) return;
        if (inFlight) { dirty = true; return; }
        if (document.hidden) { dirty = true; return; }
        inFlight = true;
        dirty = false;
        try {
            for (const region of regions) {
                const response = await fetch(region.dataset.refreshUrl, {
                    credentials: "same-origin", cache: "no-store",
                    headers: { "X-Plan-Catalog": "true", Accept: "text/html" },
                    signal: AbortSignal.timeout(10000),
                });
                if (response.status === 401 || response.status === 403 || response.redirected) {
                    stopped = true;
                    events?.close();
                    return;
                }
                if (!response.ok) continue;
                const text = await response.text();
                const doc = new DOMParser().parseFromString(text, "text/html");
                if (!doc.querySelector("[data-plan-fragment]")) continue;
                if (snapshots.get(region) === text) continue;
                snapshots.set(region, text);
                region.replaceChildren(...doc.body.childNodes);
            }
        } catch { /* The next notification, poll or reconnect re-reads authoritative state. */ }
        finally {
            inFlight = false;
            if (dirty && !document.hidden) { dirty = false; void refresh(); }
        }
    };
    const connect = () => {
        if (stopped || !("EventSource" in window) || events) return;
        events = new EventSource(regions[0].dataset.eventsUrl);
        events.addEventListener("catalog", refresh);
    };
    connect();
    // Polling also covers unsupported/buffered SSE and changes saved on another web instance.
    setInterval(() => { void refresh(); }, 15000);
    document.addEventListener("visibilitychange", () => { if (!document.hidden) void refresh(); });
    window.addEventListener("online", refresh);
    window.addEventListener("pageshow", () => { connect(); void refresh(); });
    window.addEventListener("pagehide", () => { events?.close(); events = undefined; });
})();
