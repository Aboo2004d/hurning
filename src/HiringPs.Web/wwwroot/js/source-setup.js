// اقتراح مستقل ومعاينة فقط؛ تطبيق النتيجة يملأ الحقول ولا يحفظ أو يفعّل المصدر.
(() => {
    const panel = document.querySelector("[data-source-setup]");
    if (!panel) return;
    const form = panel.querySelector("[data-setup-form]");
    const status = panel.querySelector("[data-setup-status]");
    const result = panel.querySelector("[data-setup-result]");
    const submit = panel.querySelector("[data-setup-submit]");
    const editor = document.getElementById("source-editor");
    const errors = Object.fromEntries(Array.from(panel.querySelectorAll("[data-code]"), el => [el.dataset.code, el.textContent]));
    let proposal = null;
    const website = form.querySelector('[name="url"]');
    website.addEventListener("input", () => {
        proposal = null;
        result.hidden = true;
        status.textContent = "";
    });
    form.addEventListener("submit", async event => {
        event.preventDefault();
        if (submit.disabled || !form.reportValidity()) return;
        proposal = null;
        result.hidden = true;
        submit.disabled = true;
        website.readOnly = true;
        panel.setAttribute("aria-busy", "true");
        status.textContent = status.dataset.loading;
        try {
            const response = await fetch(form.action, {
                method: "POST", body: new FormData(form),
                headers: { Accept: "application/json" }, credentials: "same-origin"
            });
            const body = await response.text();
            let data;
            try { data = JSON.parse(body); }
            catch { throw new Error((errors.SourceSetupHttpFailed || status.dataset.failed).replace("{0}", String(response.status))); }
            if (!response.ok) {
                let message = (errors[data.error] || status.dataset.failed).replace("{0}", data.sourceName || "");
                if (data.detail) message += "\n" + data.detail;
                if (data.stage && errors[data.stage]) message += " — " + errors[data.stage];
                if (data.reference) message += " — " + (errors.SourceSetupRequestReference || "Request: {0}").replace("{0}", data.reference);
                throw new Error(message);
            }
            if (data.verified !== true || !data.preview?.links?.length || !data.preview.sampleText?.trim())
                throw new Error(errors.SourceSetupNoLinks || status.dataset.failed);
            proposal = data;
            panel.querySelector("[data-setup-explanation]").textContent = data.explanation;
            panel.querySelector("[data-setup-listing]").textContent = data.url;
            const option = editor.querySelector(`[name="Connector"] option[value="${data.connector}"]`);
            panel.querySelector("[data-setup-connector]").textContent = option?.textContent || "—";
            panel.querySelector("[data-setup-next]").textContent = data.preview.next || "—";
            const links = panel.querySelector("[data-setup-links]");
            links.replaceChildren();
            data.preview.links.forEach(url => {
                const item = document.createElement("li");
                const link = document.createElement("a");
                link.href = url;
                link.textContent = url;
                link.target = "_blank";
                link.rel = "noopener noreferrer";
                item.append(link);
                links.append(item);
            });
            panel.querySelector("[data-setup-text]").textContent = data.preview.sampleText;
            panel.querySelector("[data-setup-config]").textContent = JSON.stringify(JSON.parse(data.configurationJson), null, 4);
            result.hidden = false;
            status.textContent = status.dataset.ready;
        } catch (error) {
            status.textContent = error instanceof SyntaxError ? status.dataset.failed : (error.message || status.dataset.failed);
        } finally {
            submit.disabled = false;
            website.readOnly = false;
            panel.removeAttribute("aria-busy");
        }
    });
    panel.querySelector("[data-setup-apply]").addEventListener("click", () => {
        if (!proposal || proposal.verified !== true) return;
        const name = editor.querySelector('[name="Name"]');
        if (!name.value.trim()) name.value = proposal.name;
        editor.querySelector('[name="Url"]').value = proposal.url;
        const connector = editor.querySelector('[name="Connector"]');
        connector.value = String(proposal.connector);
        connector.dispatchEvent(new Event("change", { bubbles: true }));
        editor.querySelector('[name="ConfigurationJson"]').value = JSON.stringify(JSON.parse(proposal.configurationJson), null, 4);
        status.textContent = status.dataset.applied;
        editor.scrollIntoView({ behavior: "smooth", block: "start" });
        name.focus({ preventScroll: true });
    });
})();
