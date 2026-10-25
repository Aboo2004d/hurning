// Queue the original page view before loading the external library.
window.dataLayer = window.dataLayer || [];
function gtag() { window.dataLayer.push(arguments); }
gtag('js', new Date());
gtag('config', 'G-TTF6JQSFVP', { page_location: window.location.href });

(() => {
    let started = false;
    const events = ['pointerdown', 'keydown', 'pagehide'];
    const start = () => {
        if (started) return;
        started = true;
        events.forEach(name => window.removeEventListener(name, start));
        const script = document.createElement('script');
        script.async = true;
        script.fetchPriority = 'low';
        script.src = 'https://www.googletagmanager.com/gtag/js?id=G-TTF6JQSFVP';
        document.head.appendChild(script);
    };
    // Keep analytics outside the initial rendering path; early interaction starts it sooner.
    events.forEach(name => window.addEventListener(name, start, { once: true }));
    const schedule = () => {
        if ('requestIdleCallback' in window) window.requestIdleCallback(start, { timeout: 1500 });
        else window.setTimeout(start, 0);
    };
    if (document.readyState === 'complete') schedule();
    else window.addEventListener('load', schedule, { once: true });
})();
