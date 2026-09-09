// Drag-and-drop farm card reordering - re-initialized after every live-refresh re-render since #farmCardsContainer is replaced wholesale each tick.
let farmSortable = null;
window.farmDragInProgress = false;

function initFarmDragReorder() {
    const container = document.getElementById('farmCardsContainer');
    if (!container || typeof Sortable === 'undefined') {
        return;
    }
    if (farmSortable) {
        farmSortable.destroy();
        farmSortable = null;
    }

    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';

    farmSortable = Sortable.create(container, {
        handle: '.farm-drag-handle',
        animation: 150,
        onStart: function () {
            window.farmDragInProgress = true;
        },
        onEnd: async function () {
            window.farmDragInProgress = false;
            const orderedFarmIds = Array.from(container.children)
                .map(function (el) { return parseInt(el.dataset.idDeviceFarm, 10); })
                .filter(function (id) { return !Number.isNaN(id); });
            try {
                await fetch('/DeviceFarmUnit/FarmsReorder', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
                    body: JSON.stringify(orderedFarmIds),
                });
            } catch {
                // next live-refresh tick re-renders in the still-correct server order, so a failed save just snaps back
            }
        },
    });
}

document.addEventListener('DOMContentLoaded', initFarmDragReorder);
