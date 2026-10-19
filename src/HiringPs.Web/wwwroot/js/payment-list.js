// Fetch one server-side page of payment rows, retaining filters and normal links.
(() => {
    const results = document.querySelector("[data-payment-results]");
    if (!results) return;

    let loading = false;
    results.addEventListener("click", async (event) => {
        const link = event.target.closest(".pagination a");
        if (!link || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey)
            return;

        event.preventDefault();
        if (loading) return;
        loading = true;
        results.setAttribute("aria-busy", "true");
        try {
            const response = await fetch(link.href, {
                headers: { "X-Payment-Page": "true", Accept: "text/html" },
                credentials: "same-origin",
                cache: "no-store",
            });
            if (!response.ok || response.redirected || !response.headers.get("content-type")?.includes("text/html"))
                throw new Error("Payment page unavailable");

            const page = new DOMParser().parseFromString(await response.text(), "text/html");
            const updated = page.querySelector("[data-payment-results]");
            if (!updated) throw new Error("Payment table missing");

            history.pushState(null, "", link.href);
            results.replaceChildren(...updated.childNodes);
            results.focus({ preventScroll: true });
        } catch {
            // A regular request also handles expired authentication and network errors.
            location.assign(link.href);
        } finally {
            loading = false;
            results.removeAttribute("aria-busy");
        }
    });

    // Restore the matching server-rendered page when using browser Back/Forward.
    window.addEventListener("popstate", () => location.reload());
})();
