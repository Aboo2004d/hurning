(() => {
    const tabs = document.getElementById('opportunity-tabs');
    const results = document.getElementById('opportunity-results');
    if (!tabs || !results) return;
    let request;
    let generation = 0;
    async function load(link, tab) {
        request?.abort();
        request = new AbortController();
        const id = ++generation;
        results.setAttribute('aria-busy', 'true');
        const error = document.getElementById('opportunity-load-error');
        error.hidden = true;
        try {
            const response = await fetch(link.href, { headers: { 'X-Requested-With': 'fetch' }, signal: request.signal });
            if (!response.ok || response.redirected) throw new Error('load');
            const html = await response.text();
            if (id !== generation) return;
            results.innerHTML = html;
            for (const count of results.querySelectorAll('[data-tab-count]')) {
                const label = tabs.querySelector(`[data-tab-total="${count.dataset.tabCount}"]`);
                if (label) label.textContent = count.dataset.count;
            }
            history.replaceState(null, '', link.href);
            if (tab) for (const item of tabs.querySelectorAll('a')) {
                item.classList.toggle('is-current', item === link);
                if (item === link) item.setAttribute('aria-current', 'page');
                else item.removeAttribute('aria-current');
            }
        } catch (e) { if (e.name !== 'AbortError' && id === generation) error.hidden = false; }
        finally { if (id === generation) results.removeAttribute('aria-busy'); }
    }
    tabs.addEventListener('click', e => {
        const link = e.target.closest('a');
        if (link && !e.ctrlKey && !e.metaKey) { e.preventDefault(); load(link, true); }
    });
    results.addEventListener('click', e => {
        const link = e.target.closest('.pagination a');
        if (link && !e.ctrlKey && !e.metaKey) { e.preventDefault(); load(link, false); }
    });
})();
