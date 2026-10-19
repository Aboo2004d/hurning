// Server-rendered updates only. No provider request, OTP replay, or automatic POST retry.
(() => {
    if (location.hash === "#payment-support") {
        const support = document.getElementById("payment-support");
        if (support) support.open = true;
    }
    document.addEventListener("submit", async (event) => {
        const form = event.target.closest("[data-checkout-form]");
        if (!form) return;
        const region = form.closest("[data-checkout-region]");
        if (!region) return;
        event.preventDefault();
        if (region.getAttribute("aria-busy") === "true") return;

        const data = new FormData(form);
        if (event.submitter?.name) data.set(event.submitter.name, event.submitter.value);
        const controls = [...region.querySelectorAll("input, select, textarea, button")];
        const disabled = controls.map((control) => control.disabled);
        region.setAttribute("aria-busy", "true");
        controls.forEach((control) => { control.disabled = true; });
        try {
            const response = await fetch(form.action, {
                method: "POST",
                headers: { "X-Checkout-Async": "true", Accept: "text/html, application/json" },
                body: data,
                credentials: "same-origin",
                cache: "no-store",
            });
            if (!response.ok || response.redirected) throw new Error("Checkout response unavailable");
            if (response.headers.get("content-type")?.includes("application/json")) {
                const result = await response.json();
                const destination = new URL(result.redirectUrl, location.href);
                if (!result.redirectUrl || destination.origin !== location.origin) throw new Error("Invalid checkout destination");
                location.assign(destination.href);
                return;
            }
            const fragment = new DOMParser().parseFromString(await response.text(), "text/html");
            const updated = fragment.querySelector("[data-checkout-region]");
            if (!updated) throw new Error("Checkout fragment missing");
            region.className = updated.className;
            region.replaceChildren(...updated.childNodes);
            region.focus({ preventScroll: true });
        } catch {
            if (form.hasAttribute("data-wallet-form")) {
                // A confirm may already have debited. Re-read the saved claim using GET;
                // never re-enable the old OTP form or repeat its POST automatically.
                location.replace(form.dataset.refreshUrl);
                return;
            }
            const notice = document.createElement("p");
            notice.className = "validation";
            notice.setAttribute("role", "alert");
            notice.textContent = region.dataset.requestError;
            region.prepend(notice);
            controls.forEach((control, index) => { control.disabled = disabled[index]; });
        } finally {
            region.removeAttribute("aria-busy");
        }
    });
})();
