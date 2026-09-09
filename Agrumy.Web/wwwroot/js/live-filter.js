// Client-side substring filter over one container's children - no page refresh, no server round trip. Re-run after any live-refresh re-render since the input and its target container get replaced wholesale.
function wireLiveFilters(inputSelector, itemSelector) {
    document.querySelectorAll(inputSelector).forEach(function (input) {
        var container = document.getElementById(input.dataset.filterTarget);
        if (!container) {
            return;
        }
        input.addEventListener('input', function () {
            var query = input.value.trim().toLowerCase();
            container.querySelectorAll(itemSelector).forEach(function (el) {
                var text = el.dataset.filterValue || '';
                el.hidden = query.length > 0 && text.indexOf(query) === -1;
            });
        });
    });
}
