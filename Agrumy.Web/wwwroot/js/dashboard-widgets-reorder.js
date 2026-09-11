// Drag-and-drop widget reorder, same POST-on-drop idiom as farms-reorder.js; the payload carries OLD LIST INDICES (widgets have no id of their own), not entity ids.
(function () {
    var container = document.getElementById('dashboardWidgetsGrid');
    if (!container || typeof Sortable === 'undefined') {
        return;
    }
    var token = document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';

    Sortable.create(container, {
        handle: '.widget-drag-handle',
        animation: 150,
        onEnd: async function () {
            var order = Array.from(container.children)
                .map(function (el) { return parseInt(el.dataset.index, 10); })
                .filter(function (i) { return !Number.isNaN(i); });
            var body = {};
            body[container.dataset.leafField] = parseInt(container.dataset.leafId, 10);
            try {
                await fetch(container.dataset.reorderAction + '?' + container.dataset.leafField + '=' + container.dataset.leafId, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
                    body: JSON.stringify(order),
                });
            } catch {
                // next page load re-renders in the still-correct server order, same fallback as farms-reorder.js
            }
        },
    });
})();
