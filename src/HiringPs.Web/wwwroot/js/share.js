// Copy-link button for the opportunity share panel. Share links themselves work without JavaScript.
(() => {
    document.querySelectorAll("[data-copy-link]").forEach(button => {
        if (!navigator.clipboard) return;
        button.hidden = false;
        const feedback = button.parentElement.querySelector("[data-copy-feedback]");
        button.addEventListener("click", async () => {
            try {
                await navigator.clipboard.writeText(button.dataset.copyLink);
                if (feedback) feedback.textContent = button.dataset.copiedText;
            } catch {
                if (feedback) feedback.textContent = button.dataset.copyError;
            }
        });
    });
})();
