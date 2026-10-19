(() => {
    document.addEventListener('change', e => {
        if (!e.target.matches('[data-social-field]')) return;
        const section = e.target.closest('[data-social-compose]');
        section.querySelector('[data-social-text]').value = [...section.querySelectorAll('[data-social-field]:checked')].map(x => x.dataset.text).filter(Boolean).join('\n');
    });
    document.addEventListener('submit', e => {
        if (!e.target.closest('[data-social-compose]')) return;
        const button = e.target.querySelector('button[type="submit"]');
        if (button) button.disabled = true;
    });
})();
