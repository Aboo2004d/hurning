(() => {
    document.addEventListener('click', async event => {
        const token = event.target.closest('[data-template-token]');
        if (token) {
            const input = token.closest('[data-template-editor]').querySelector('[data-template-body]');
            input.setRangeText('{{' + token.dataset.templateToken + '}}', input.selectionStart, input.selectionEnd, 'end');
            input.focus();
            return;
        }
        const load = event.target.closest('[data-template-load]');
        if (load) {
            const input = document.querySelector('[data-template-body]');
            input.value = load.closest('details').querySelector('pre').textContent;
            input.focus();
            return;
        }
        const preview = event.target.closest('[data-template-preview]');
        if (preview) {
            const section = preview.closest('[data-template-editor]');
            const error = section.querySelector('[data-template-error]');
            const data = new FormData(section.querySelector('[data-template-form]'));
            data.set('body', section.querySelector('[data-template-body]').value);
            data.set('opportunityId', section.querySelector('[data-template-opportunity]').value);
            preview.disabled = true;
            error.hidden = true;
            try {
                const response = await fetch(preview.dataset.url, { method: 'POST', body: data, credentials: 'same-origin' });
                if (!response.ok || response.redirected) throw new Error();
                section.querySelector('[data-template-output]').textContent = (await response.json()).text;
            } catch { error.hidden = false; }
            finally { preview.disabled = false; }
            return;
        }
        const link = event.target.closest('[data-template-list] a');
        if (!link || !link.closest('nav') || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
        const region = link.closest('[data-template-list]');
        event.preventDefault();
        try {
            const response = await fetch(link.href, { credentials: 'same-origin', cache: 'no-store' });
            if (!response.ok || response.redirected) throw new Error();
            const documentResult = new DOMParser().parseFromString(await response.text(), 'text/html');
            const replacement = documentResult.querySelector('[data-template-list]');
            if (!replacement) throw new Error();
            region.replaceChildren(...replacement.childNodes);
            history.replaceState(null, '', link.href);
        } catch { window.location.assign(link.href); }
    });
})();
