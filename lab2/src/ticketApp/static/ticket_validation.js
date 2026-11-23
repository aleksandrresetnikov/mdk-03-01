document.addEventListener('DOMContentLoaded', () => {
    const form = document.getElementById('ticket_form');
    if (!form) return;

    // Django Admin отключает HTML5-валидацию через novalidate. Включаем её.
    form.noValidate = false;
    const rules = [['title', 5, 100], ['description', 10, 2000]];

    for (const [name, minimum, maximum] of rules) {
        const input = form.elements.namedItem(name);
        if (!input) continue;
        const hint = document.createElement('p');
        hint.className = 'help lab2-client-error';
        hint.style.color = '#ba2121';
        hint.style.marginLeft = '160px';
        hint.style.display = 'none';
        hint.setAttribute('aria-live', 'polite');
        input.closest('.flex-container').after(hint);

        function validate() {
            const length = input.value.trim().length;
            const error = length < minimum || length > maximum
                ? `Введите от ${minimum} до ${maximum} символов без крайних пробелов.`
                : '';
            input.setCustomValidity(error);
            hint.textContent = error;
            hint.style.display = error ? 'block' : 'none';
        }

        input.addEventListener('input', validate);
        input.addEventListener('invalid', validate);
    }
});
