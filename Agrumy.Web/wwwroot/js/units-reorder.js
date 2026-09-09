// Drag-and-drop unit cube reordering, same concept as farms-reorder.js applied at Unit level - one Sortable instance per [data-unit-cube-container] (a farm's units, or the unassigned bucket), re-initialized after every live-refresh re-render since the containers are replaced wholesale.
var unitSortables = [];
window.unitDragInProgress = false;

function initUnitDragReorder() {
    unitSortables.forEach(function (s) { s.destroy(); });
    unitSortables = [];

    if (typeof Sortable === 'undefined') {
        return;
    }
    var token = document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';

    document.querySelectorAll('[data-unit-cube-container]').forEach(function (container) {
        unitSortables.push(Sortable.create(container, {
            handle: '.unit-drag-handle',
            animation: 150,
            onStart: function () {
                window.unitDragInProgress = true;
            },
            onEnd: async function () {
                window.unitDragInProgress = false;
                var orderedUnitIds = Array.from(container.children)
                    .map(function (el) { return parseInt(el.dataset.idDeviceFarmUnit, 10); })
                    .filter(function (id) { return !Number.isNaN(id); });
                try {
                    await fetch('/DeviceFarmUnit/UnitsReorder', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
                        body: JSON.stringify(orderedUnitIds),
                    });
                } catch {
                    // next live-refresh tick re-renders in the still-correct server order, so a failed save just snaps back
                }
            },
        }));
    });
}

document.addEventListener('DOMContentLoaded', initUnitDragReorder);
