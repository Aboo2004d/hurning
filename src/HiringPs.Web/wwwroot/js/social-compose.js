(() => {
    document.addEventListener('submit', e => {
        if (!e.target.closest('[data-social-compose]')) return;
        const button = e.target.querySelector('button[type="submit"]');
        if (button) button.disabled = true;
    });
})();
