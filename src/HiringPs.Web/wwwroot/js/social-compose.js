(() => {
    document.addEventListener('submit', e => {
        if (!e.target.closest('[data-social-compose]')) return;
        const targets = e.target.querySelector('[data-social-targets]');
        if (targets && !targets.querySelector('input[name="Targets"]:checked:not(:disabled)')) {
            e.preventDefault();
            let error = targets.querySelector('[role="alert"]');
            if (!error) {
                error = document.createElement('p');
                error.setAttribute('role', 'alert');
                targets.append(error);
            }
            error.textContent = targets.dataset.requiredMessage;
            targets.querySelector('input:not(:disabled)')?.focus();
            return;
        }
        const button = e.target.querySelector('button[type="submit"]');
        if (button) button.disabled = true;
    });
})();
