// Filtering changes only visibility; checked members remain submitted and visible for review.
document.addEventListener('input', event => {
    if (!event.target.matches('[data-category-group-filter]')) return;
    const normalize = value => value.toLowerCase().normalize().replace(/[أإآ]/gu, 'ا').trim();
    const value = normalize(event.target.value);
    event.target.closest('[data-category-group-editor]').querySelectorAll('[data-group-choice]').forEach(choice => {
        choice.hidden = !choice.querySelector('input').checked && !normalize(choice.dataset.label).includes(value);
    });
});
