(() => {
    const feedback = document.querySelector("[data-tools-feedback]");
    if (!feedback) return;
    let timer;
    let busy = false;
    const dashboard = () => document.querySelector("[data-tools-dashboard]");
    const notify = key => {
        feedback.textContent = feedback.dataset[key];
        feedback.hidden = false;
    };
    const schedule = () => {
        clearTimeout(timer);
        if (dashboard()?.dataset.pending === "true") timer = setTimeout(poll, 5000);
    };
    async function refresh() {
        const response = await fetch(location.href, {
            cache: "no-store", credentials: "same-origin", signal: AbortSignal.timeout(15000)
        });
        if (!response.ok || response.redirected) throw new Error("Dashboard unavailable");
        const page = new DOMParser().parseFromString(await response.text(), "text/html");
        const next = page.querySelector("[data-tools-dashboard]");
        if (!next) throw new Error("Invalid dashboard response");
        // Apply the same mobile table labels as site.js to the refreshed markup.
        for (const table of next.querySelectorAll(".table-wrap table")) {
            table.classList.add("admin-table");
            const headings = Array.from(table.querySelectorAll("thead th"), th => th.textContent.trim());
            for (const row of table.querySelectorAll("tbody tr"))
                Array.from(row.cells).forEach((cell, index) => { cell.dataset.label = headings[index] || ""; });
        }
        // Keep keyboard focus when replacing the dashboard after a check.
        const focusedTool = document.activeElement?.closest("[data-tool-check]")
            ?.querySelector('[name="tool"]')?.value;
        dashboard().replaceWith(next);
        if (focusedTool) {
            for (const form of next.querySelectorAll("[data-tool-check]")) {
                if (form.querySelector('[name="tool"]').value === focusedTool)
                    form.querySelector("button").focus({ preventScroll: true });
            }
        }
    }
    async function poll() {
        if (busy) { schedule(); return; }
        busy = true;
        try {
            await refresh();
        } catch {
            notify("error");
        } finally {
            busy = false;
            schedule();
        }
    }
    document.addEventListener("submit", async event => {
        const form = event.target.closest("[data-tool-check]");
        if (!form) return;
        event.preventDefault();
        if (busy) return;
        busy = true;
        clearTimeout(timer);
        const button = form.querySelector("button");
        button.disabled = true;
        form.setAttribute("aria-busy", "true");
        try {
            const url = new URL(form.action, location.href);
            for (const [key, value] of new FormData(form)) url.searchParams.set(key, value);
            const response = await fetch(url, {
                headers: { "X-Requested-With": "XMLHttpRequest", "Accept": "application/json" },
                cache: "no-store", credentials: "same-origin", signal: AbortSignal.timeout(15000)
            });
            if (!response.ok || response.redirected) throw new Error("Check request failed");
            const result = await response.json();
            if (typeof result.queued !== "boolean") throw new Error("Invalid check response");
            notify(result.queued ? "queued" : "existing");
            await refresh();
        } catch {
            // Do not repeat the command automatically: it may already be queued.
            notify("error");
        } finally {
            button.disabled = false;
            form.removeAttribute("aria-busy");
            busy = false;
            schedule();
        }
    });
    schedule();
})();
