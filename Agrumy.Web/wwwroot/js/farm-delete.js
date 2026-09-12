// Farm delete is now a real cascade (units/zones/devices go with it, into the
// Recycle Bin), so this confirms and, when another farm exists, offers migrating units out first.
document.addEventListener('DOMContentLoaded', function () {
    const modalEl = document.getElementById('farmDeleteModal');
    if (!modalEl) {
        return;
    }
    const modal = new bootstrap.Modal(modalEl);
    const migrateSection = document.getElementById('farmDeleteMigrateSection');
    const migrateTarget = document.getElementById('farmDeleteMigrateTarget');
    const migrateButton = document.getElementById('farmDeleteMigrateButton');
    const nameLabel = document.getElementById('farmDeleteNameLabel');
    const confirmButton = document.getElementById('farmDeleteConfirmButton');
    const deleteForm = document.getElementById('farmDeleteForm');
    const deleteIdInput = document.getElementById('farmDeleteId');
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';

    let currentFarmId = null;

    // Delegated on document, not attached per-button - the button lives inside the 10s live-refreshed
    // #farmsAndUnits area, so a direct per-button listener would stop working the moment innerHTML
    // swaps in a freshly-rendered button with no listener attached.
    document.addEventListener('click', function (event) {
        const button = event.target.closest('.farm-delete-button');
        if (!button) {
            return;
        }
        currentFarmId = button.dataset.idDeviceFarm;
        nameLabel.textContent = button.dataset.farmName;
        deleteIdInput.value = currentFarmId;
        migrateTarget.value = '';
        migrateSection.style.display = Number(button.dataset.otherFarmCount) > 0 ? 'block' : 'none';
        modal.show();
    });

    migrateButton.addEventListener('click', async function () {
        if (!migrateTarget.value) {
            return;
        }
        migrateButton.disabled = true;
        try {
            const response = await fetch('/DeviceFarmUnit/FarmMigrateUnits', {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'RequestVerificationToken': token },
                body: 'idDeviceFarm=' + encodeURIComponent(currentFarmId) + '&idTargetFarm=' + encodeURIComponent(migrateTarget.value),
            });
            if (response.ok) {
                window.location.reload();
            }
        } finally {
            migrateButton.disabled = false;
        }
    });

    confirmButton.addEventListener('click', function () {
        deleteForm.requestSubmit();
    });
});
