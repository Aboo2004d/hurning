// Existing names are suggested, never substituted without an explicit choice.
(() => {
    const clean = value => value.trim().replace(/\s+/gu, ' ');
    const key = value => clean(value).toLowerCase().normalize().replace(/[أإآ]/gu, 'ا');
    function distance(a, b) {
        let previous = Array.from({length: b.length + 1}, (_, i) => i);
        for (let i = 1; i <= a.length; i++) {
            const current = [i];
            for (let j = 1; j <= b.length; j++) current[j] = Math.min(current[j - 1] + 1, previous[j] + 1, previous[j - 1] + (a[i - 1] === b[j - 1] ? 0 : 1));
            previous = current;
        }
        return previous[b.length];
    }
    function score(value, query) {
        if (!query) return 1;
        const text = key(value);
        if (text === query) return 0;
        if (text.startsWith(query)) return 1;
        if (text.includes(query)) return 2;
        // Bound fuzzy work for unusually large historic source values.
        if (query.length < 3 || text.length > 200 || query.length > 200) return Infinity;
        const ratio = distance(text, query) / Math.max(text.length, query.length);
        return ratio <= .4 ? 3 + ratio : Infinity;
    }
    function adminSuggestions(input) {
        const picker = input.closest('[data-category-picker]');
        const list = picker.querySelector('[role="listbox"]');
        input.removeAttribute('list'); // Native datalist remains a no-JavaScript fallback.
        list.replaceChildren();
        const query = key(input.value);
        const options = [...picker.querySelectorAll('datalist option')].map(option => option.value);
        const matches = options.map(value => ({value, score: score(value, query)}))
            .filter(item => Number.isFinite(item.score)).sort((a, b) => a.score - b.score || a.value.localeCompare(b.value)).slice(0, 8);
        const add = (value, label) => {
            const option = document.createElement('button');
            option.type = 'button'; option.setAttribute('role', 'option'); option.tabIndex = -1;
            option.textContent = label; option.dataset.categoryValue = value;
            list.append(option);
        };
        matches.forEach(item => add(item.value, item.value));
        if (query && !options.some(value => key(value) === query)) add(clean(input.value), picker.dataset.newLabel.replace('{0}', clean(input.value)));
        list.hidden = list.children.length === 0;
        input.setAttribute('aria-expanded', String(!list.hidden));
        input.removeAttribute('aria-activedescendant');
    }
    function memberSuggestions(input) {
        const section = input.closest('.preference-group');
        const container = section.querySelector('[data-preference-options]');
        const query = key(input.value);
        const items = [...container.querySelectorAll('[data-preference-value]')].map(node => ({node, score: score(node.dataset.preferenceValue, query)}))
            .sort((a, b) => a.score - b.score || a.node.dataset.preferenceValue.localeCompare(b.node.dataset.preferenceValue));
        items.forEach(item => { item.node.hidden = !Number.isFinite(item.score); container.append(item.node); });
        const add = section.querySelector('[data-preference-new-category]');
        const values = [...section.querySelectorAll('[name="Categories"]')].map(node => node.value)
            .concat(items.map(item => item.node.dataset.preferenceValue));
        add.value = clean(input.value);
        add.textContent = add.dataset.newLabel.replace('{0}', add.value);
        add.hidden = !query || values.some(value => key(value) === query);
    }
    document.addEventListener('input', event => {
        if (event.target.matches('[data-category-picker] input')) adminSuggestions(event.target);
        if (event.target.matches('[data-preference-category-search]')) memberSuggestions(event.target);
    });
    document.addEventListener('focusin', event => {
        if (event.target.matches('[data-category-picker] input')) adminSuggestions(event.target);
    });
    document.addEventListener('click', event => {
        const option = event.target.closest('[data-category-value]');
        if (option) {
            const picker = option.closest('[data-category-picker]');
            const input = picker.querySelector('input');
            input.value = option.dataset.categoryValue;
            input.focus();
            picker.querySelector('[role="listbox"]').hidden = true;
            input.setAttribute('aria-expanded', 'false');
            input.removeAttribute('aria-activedescendant');
        }
        document.querySelectorAll('[data-category-picker]').forEach(picker => {
            if (!picker.contains(event.target)) {
                picker.querySelector('[role="listbox"]').hidden = true;
                picker.querySelector('input').setAttribute('aria-expanded', 'false');
            }
        });
    });
    document.addEventListener('keydown', event => {
        const picker = event.target.closest('[data-category-picker]');
        if (!picker) return;
        const input = picker.querySelector('input');
        const list = picker.querySelector('[role="listbox"]');
        if (event.key === 'Escape') { list.hidden = true; input.setAttribute('aria-expanded', 'false'); input.removeAttribute('aria-activedescendant'); return; }
        if (event.key === 'Enter' && !list.hidden && input.getAttribute('aria-activedescendant')) {
            event.preventDefault(); document.getElementById(input.getAttribute('aria-activedescendant'))?.click(); return;
        }
        if (!['ArrowDown', 'ArrowUp'].includes(event.key)) return;
        event.preventDefault();
        if (list.hidden) adminSuggestions(input);
        const choices = [...list.children];
        if (!choices.length) return;
        const active = choices.findIndex(option => option.id === input.getAttribute('aria-activedescendant'));
        const next = active < 0 ? (event.key === 'ArrowDown' ? 0 : choices.length - 1)
            : (active + (event.key === 'ArrowDown' ? 1 : -1) + choices.length) % choices.length;
        choices.forEach((option, i) => { option.id = `${list.id}-${i}`; option.setAttribute('aria-selected', String(i === next)); });
        input.setAttribute('aria-activedescendant', choices[next].id);
    });
})();
