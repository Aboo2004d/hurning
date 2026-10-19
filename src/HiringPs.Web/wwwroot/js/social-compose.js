(() => {
    document.addEventListener('change', e => {
        if (!e.target.matches('[data-social-field]')) return;
        const section = e.target.closest('[data-social-compose]');
        section.querySelector('[data-social-text]').value = [...section.querySelectorAll('[data-social-field]:checked')].map(x => x.dataset.text).filter(Boolean).join('\n');
    });
    document.addEventListener('click', async e => {
        const link = e.target.closest('[data-social-open]');
        if (!link || e.ctrlKey || e.metaKey) return;
        const dialog = document.getElementById('social-dialog');
        if (!dialog?.showModal) return;
        e.preventDefault();
        const host = dialog.querySelector('[data-social-host]');
        link.setAttribute('aria-busy', 'true');
        try {
            const response = await fetch(link.href, { headers: { 'X-Requested-With': 'fetch' } });
            if (!response.ok || response.redirected) throw new Error();
            host.innerHTML = await response.text();
            dialog.showModal();
        } catch { window.location.assign(link.href); }
        finally { link.removeAttribute('aria-busy'); }
    });
    document.addEventListener('submit', e => {
        if (!e.target.closest('[data-social-compose]')) return;
        const button = e.target.querySelector('button[type="submit"]');
        if (button) button.disabled = true;
    });
})();
