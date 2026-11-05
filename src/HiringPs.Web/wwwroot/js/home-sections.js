(() => {
    'use strict';
    const sections = [...document.querySelectorAll('[data-home-section]')];
    async function load(section, refresh = false) {
        if (section.dataset.state === 'loading' || (section.dataset.state === 'loaded' && !refresh)) return;
        section.dataset.state = 'loading';
        section.dataset.refreshRequested = String(refresh);
        section.setAttribute('aria-busy', 'true');
        const loading = section.querySelector('.home-section-loading');
        const error = section.querySelector('.home-section-error');
        const results = section.querySelector('[data-section-results]');
        const refreshButton = section.querySelector('[data-section-refresh]');
        refreshButton.disabled = true;
        results.hidden = true;
        loading.hidden = false;
        error.hidden = true;
        const manual = section.querySelector('[data-section-load]');
        if (manual) manual.hidden = true;
        const controller = new AbortController();
        const timer = setTimeout(() => controller.abort(), 25000);
        try {
            const url = new URL(section.dataset.url, window.location.href);
            if (refresh) { url.searchParams.set('refresh', 'true'); url.searchParams.set('_', Date.now().toString()); }
            const response = await fetch(url, {
                signal: controller.signal, credentials: 'same-origin', headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });
            if (!response.ok || response.redirected) throw new Error('Section request failed');
            results.innerHTML = await response.text();
            if (!results.textContent.trim()) results.textContent = results.dataset.emptyText;
            results.hidden = false;
            loading.hidden = true;
            section.dataset.state = 'loaded';
        } catch {
            section.dataset.state = 'failed';
            loading.hidden = true;
            error.hidden = false;
        } finally {
            clearTimeout(timer);
            section.setAttribute('aria-busy', 'false');
            refreshButton.disabled = false;
        }
    }
    sections.forEach(section => {
        section.querySelector('[data-section-retry]').addEventListener('click', () => load(section, section.dataset.refreshRequested === 'true'));
        section.querySelector('[data-section-refresh]').addEventListener('click', () => load(section, true));
        section.querySelector('[data-section-load]')?.addEventListener('click', () => load(section));
    });
    async function start() {
        // Sequence even after a failure: a slow section must not block the others permanently.
        for (const section of sections.filter(x => x.dataset.lazy !== 'true')) await load(section);
        const lazy = sections.filter(x => x.dataset.lazy === 'true');
        if ('IntersectionObserver' in window) {
            // Serialize bottom sections too, to avoid a cold-cache stampede and concurrent queries.
            let queue = Promise.resolve();
            const observer = new IntersectionObserver(entries => {
                for (const entry of entries) if (entry.isIntersecting) {
                    observer.unobserve(entry.target);
                    queue = queue.then(() => load(entry.target));
                }
            }, { rootMargin: '200px 0px' });
            lazy.forEach(section => observer.observe(section));
        } else {
            lazy.forEach(section => {
                section.setAttribute('aria-busy', 'false');
                section.querySelector('.home-section-loading').hidden = true;
                section.querySelector('[data-section-load]').hidden = false;
            });
        }
    }
    if (document.readyState === 'complete') start();
    else window.addEventListener('load', start, { once: true });
})();
