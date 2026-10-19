// Member actions stay on the current page. Mutations retain their ordinary protected POST forms.
(() => {
    const header = { "X-Member-Async": "true" };
    const busy = new WeakSet();

    function showError(region) {
        region.querySelector("[data-member-error]")?.remove();
        const message = document.createElement("p");
        message.className = "validation";
        message.dataset.memberError = "true";
        message.setAttribute("role", "alert");
        message.textContent = region.dataset.error;
        region.prepend(message);
    }

    async function saveOpportunity(form) {
        if (busy.has(form)) return;
        const data = new FormData(form);
        const id = data.get("id");
        const buttons = [...document.querySelectorAll("[data-save-button]")]
            .filter(button => button.dataset.saveButton === id);
        if (!buttons.length || buttons.every(button => button.disabled)) return;
        const disabled = buttons.map(button => button.disabled);
        const shouldSave = buttons[0].getAttribute("aria-pressed") !== "true";
        const action = shouldSave ? buttons[0].dataset.saveAction : buttons[0].dataset.unsaveAction;
        busy.add(form);
        buttons.forEach(button => { button.disabled = true; button.setAttribute("aria-busy", "true"); });
        const feedback = [...document.querySelectorAll("[data-save-feedback]")]
            .filter(node => node.dataset.saveFeedback === id);
        feedback.forEach(node => { node.hidden = true; });
        try {
            const response = await fetch(action, {
                method: "POST", headers: { ...header, Accept: "application/json" }, body: data,
                credentials: "same-origin", cache: "no-store",
            });
            if (!response.ok || response.redirected) throw new Error("Save unavailable");
            const result = await response.json();
            if (result.saved !== shouldSave || String(result.opportunityId) !== id) throw new Error("Unexpected saved state");
            buttons.forEach(button => {
                button.classList.toggle("is-saved", result.saved);
                button.setAttribute("aria-pressed", String(result.saved));
                button.formAction = result.saved ? button.dataset.unsaveAction : button.dataset.saveAction;
                button.querySelector(".fa-solid").className = result.saved ? "fa-solid fa-trash-can" : "fa-solid fa-bookmark";
                button.querySelector("[data-save-label]").textContent = result.saved ? button.dataset.unsaveText : button.dataset.saveText;
            });
            feedback.forEach(node => {
                node.classList.remove("is-error");
                node.textContent = result.saved ? buttons[0].dataset.savedText : buttons[0].dataset.unsavedText;
                node.hidden = false;
            });
        } catch {
            feedback.forEach(node => {
                node.classList.add("is-error");
                node.textContent = buttons[0].dataset.saveError;
                node.hidden = false;
            });
        } finally {
            busy.delete(form);
            buttons.forEach((button, index) => {
                button.disabled = disabled[index];
                button.removeAttribute("aria-busy");
            });
        }
    }

    async function updateRegion(region, url, data, selector, pushHistory = false) {
        if (busy.has(region)) return;
        const controls = [...region.querySelectorAll("input, select, textarea, button")];
        const disabled = controls.map(control => control.disabled);
        busy.add(region);
        region.setAttribute("aria-busy", "true");
        controls.forEach(control => { control.disabled = true; });
        try {
            const response = await fetch(url, {
                method: data ? "POST" : "GET", headers: { ...header, Accept: "text/html" },
                body: data || undefined, credentials: "same-origin", cache: "no-store",
            });
            if (!response.ok || response.redirected || !response.headers.get("content-type")?.includes("text/html"))
                throw new Error("Member update unavailable");
            const fragment = new DOMParser().parseFromString(await response.text(), "text/html");
            const updated = fragment.querySelector(selector);
            if (!updated) throw new Error("Member fragment missing");
            // Keep the catalog disclosures open while selecting several preferences.
            const openLists = [...region.querySelectorAll("details[data-preference-list][open]")]
                .map(list => list.dataset.preferenceList);
            updated.querySelectorAll("details[data-preference-list]").forEach(list => {
                list.open = openLists.includes(list.dataset.preferenceList);
            });
            if (updated.dataset.currentUrl) {
                const destination = new URL(updated.dataset.currentUrl, location.href);
                if (destination.origin !== location.origin) throw new Error("Invalid list destination");
                history[pushHistory ? "pushState" : "replaceState"](null, "", destination.href);
                region.dataset.currentUrl = updated.dataset.currentUrl;
            }
            region.replaceChildren(...updated.childNodes);
            region.focus({ preventScroll: true });
        } catch {
            controls.forEach((control, index) => { control.disabled = disabled[index]; });
            showError(region);
        } finally {
            busy.delete(region);
            region.removeAttribute("aria-busy");
        }
    }

    document.addEventListener("submit", event => {
        const form = event.target;
        if (form.matches("[data-save-opportunity]")) {
            event.preventDefault();
            void saveOpportunity(form);
            return;
        }
        const selector = form.matches("[data-preferences-form]") ? "[data-preference-editor]"
            : form.matches("[data-saved-remove]") ? "[data-saved-results]" : null;
        if (!selector) return;
        event.preventDefault();
        const region = form.closest(selector);
        if (!region || busy.has(region)) return;
        const data = new FormData(form);
        if (event.submitter?.name) data.set(event.submitter.name, event.submitter.value);
        void updateRegion(region, form.action, data, selector);
    }, true);

    document.addEventListener("change", event => {
        if (!event.target.matches('[data-preferences-form] input[type="checkbox"]')) return;
        const form = event.target.closest("form");
        if (!busy.has(form.closest("[data-preference-editor]"))) form.requestSubmit();
    });

    document.addEventListener("click", event => {
        const link = event.target.closest("[data-saved-results] .pagination a");
        if (!link || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
        event.preventDefault();
        void updateRegion(link.closest("[data-saved-results]"), link.href, null, "[data-saved-results]", true);
    }, true);

    if (document.querySelector("[data-saved-results]")) {
        window.addEventListener("popstate", () => location.reload());
    }
})();
