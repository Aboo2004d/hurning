// Updates only the pre-order quote. Payment/OTP pages do not load this script or marker.
(() => {
    let busy = false;
    let stopped = false;
    let events;
    const refresh = async () => {
        const region = document.querySelector("[data-plan-preview]");
        if (!region || stopped || busy || document.hidden || region.getAttribute("aria-busy") === "true") return;
        const form = region.querySelector("form");
        const values = new FormData(form);
        const signature = JSON.stringify([...values.entries()]);
        const url = new URL(region.dataset.quoteUrl, location.href);
        url.searchParams.set("id", values.get("PlanId"));
        url.searchParams.set("code", values.get("DiscountCode") || "");
        url.searchParams.set("gateway", values.get("Gateway") || "");
        url.searchParams.set("requestId", values.get("RequestId"));
        busy = true;
        try {
            const response = await fetch(url, {
                credentials: "same-origin", cache: "no-store", signal: AbortSignal.timeout(10000),
                headers: { Accept: "text/html" },
            });
            if (response.status === 401 || response.status === 403 || response.redirected) {
                stopped = true; events?.close(); return;
            }
            if (response.status === 204) { stopped = true; events?.close(); return; }
            if (response.status === 404) {
                // A hidden/removed plan is no longer purchasable; server will reject any stale submission too.
                region.querySelectorAll('button[name="checkout"][value="true"]').forEach(x => { x.disabled = true; });
                if (!region.querySelector("[data-plan-unavailable]")) {
                    const notice = document.createElement("p");
                    notice.dataset.planUnavailable = "";
                    notice.className = "notice";
                    notice.setAttribute("role", "alert");
                    notice.textContent = region.dataset.unavailableMessage;
                    region.prepend(notice);
                }
                return;
            }
            if (!response.ok) return;
            const doc = new DOMParser().parseFromString(await response.text(), "text/html");
            const updated = doc.querySelector("[data-plan-preview]");
            if (!updated || updated.dataset.quoteSignature === region.dataset.quoteSignature) return;
            // Do not overwrite edits made during the read or race an in-flight checkout POST.
            if (!region.isConnected || region.getAttribute("aria-busy") === "true"
                || JSON.stringify([...new FormData(form).entries()]) !== signature) return;
            // Fresh terms are visible before the user confirms them again; preserve the idempotency key.
            region.replaceWith(updated);
        } catch { /* The next event or poll retries this read, never the payment POST. */ }
        finally { busy = false; }
    };
    const connect = () => {
        const region = document.querySelector("[data-plan-preview]");
        if (!region || stopped || events || !("EventSource" in window)) return;
        events = new EventSource(region.dataset.eventsUrl);
        events.addEventListener("catalog", refresh);
    };
    connect();
    setInterval(() => { void refresh(); }, 15000);
    document.addEventListener("visibilitychange", () => { if (!document.hidden) void refresh(); });
    window.addEventListener("online", refresh);
    window.addEventListener("pageshow", () => { connect(); void refresh(); });
    window.addEventListener("pagehide", () => { events?.close(); events = undefined; });
})();
