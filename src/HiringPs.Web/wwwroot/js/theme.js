// Run in <head> so the saved appearance applies before page content is painted.
// Light is the default; dark applies only after the visitor chooses it.
(() => {
    const storageKey = 'hiring.theme.v1';
    const palette = document.querySelector('[data-dark-theme]');
    const valid = value => value === 'light' || value === 'dark';
    const readPreference = () => {
        try {
            const value = localStorage.getItem(storageKey);
            return valid(value) ? value : null;
        } catch { return null; }
    };
    let preference = readPreference();
    const apply = () => {
        const theme = preference || 'light';
        document.documentElement.dataset.theme = theme;
        if (palette) palette.media = theme === 'dark' ? 'all' : 'not all';
        const target = theme === 'dark' ? 'light' : 'dark';
        document.querySelectorAll('[data-theme-toggle]').forEach(button => {
            const label = button.getAttribute(`data-theme-${target}-label`);
            button.setAttribute('aria-label', label);
            button.title = label;
            button.querySelector('[data-theme-label]').textContent = label;
            button.querySelectorAll('[data-theme-icon]').forEach(icon => {
                icon.toggleAttribute('hidden', icon.getAttribute('data-theme-icon') !== target);
            });
            button.hidden = false;
        });
    };
    apply();
    document.addEventListener('DOMContentLoaded', apply, {once: true});
    document.addEventListener('click', event => {
        if (!event.target.closest('[data-theme-toggle]')) return;
        preference = document.documentElement.dataset.theme === 'dark' ? 'light' : 'dark';
        try { localStorage.setItem(storageKey, preference); } catch { /* Switching still works on this page. */ }
        apply();
    });
    window.addEventListener('storage', event => {
        if (event.key === storageKey || event.key === null) {
            preference = readPreference();
            apply();
        }
    });
})();
