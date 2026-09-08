// One shared confirm modal for all three destructive actions (per-device purge, per-farm purge,
// empty everything) - which one is pending lives in `pendingAction`, set when a trigger is clicked.
document.addEventListener('DOMContentLoaded', function () {
    const confirmModalEl = document.getElementById('recycleBinPurgeConfirmModal');
    if (!confirmModalEl) {
        return; // not a Global Admin - the page renders no purge controls at all
    }

    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';
    const status = document.getElementById('recycleBinStatus');
    const confirmModal = new bootstrap.Modal(confirmModalEl);
    const confirmTitle = document.getElementById('recycleBinPurgeConfirmTitle');
    const confirmBody = document.getElementById('recycleBinPurgeConfirmBody');
    const confirmPhraseInput = document.getElementById('recycleBinPurgeConfirmPhrase');
    const confirmSubmitButton = document.getElementById('recycleBinPurgeConfirmSubmit');

    let pendingAction = null;

    function setStatus(text, isError) {
        status.textContent = text;
        status.className = isError ? 'mb-3 text-danger' : 'mb-3 text-success';
    }

    async function postJson(url, body) {
        const response = await fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
            body: JSON.stringify(body),
        });
        const text = await response.text();
        if (!response.ok) {
            throw new Error(text || ('HTTP ' + response.status));
        }
        return text ? JSON.parse(text) : null;
    }

    function openConfirm(kind, id, name) {
        pendingAction = { kind, id };
        confirmPhraseInput.value = '';
        confirmSubmitButton.disabled = true;
        if (kind === 'device') {
            confirmTitle.textContent = 'Mark device for permanent deletion';
            confirmBody.textContent = `Marks "${name}" for permanent removal (including its sensor data) - it stays fully restorable from Pending Permanent Deletion until the purge cycle actually runs.`;
        } else if (kind === 'farm') {
            confirmTitle.textContent = 'Mark farm for permanent deletion';
            confirmBody.textContent = `Marks "${name}" and every unit/zone/device still attached to it for permanent removal (including sensor data) - it stays fully restorable from Pending Permanent Deletion until the purge cycle actually runs.`;
        } else {
            confirmTitle.textContent = 'Empty Recycle Bin';
            confirmBody.textContent = 'Marks every farm and device currently listed here for permanent removal (including sensor data) - they stay fully restorable from Pending Permanent Deletion until the purge cycle actually runs.';
        }
        confirmModal.show();
    }

    document.querySelectorAll('[data-recycle-bin-purge]').forEach(function (button) {
        button.addEventListener('click', function () {
            openConfirm(button.dataset.kind, button.dataset.id, button.dataset.name);
        });
    });

    const emptyButton = document.getElementById('recycleBinEmptyButton');
    if (emptyButton) {
        emptyButton.addEventListener('click', function () {
            openConfirm('empty', null, null);
        });
    }

    confirmPhraseInput.addEventListener('input', function () {
        confirmSubmitButton.disabled = confirmPhraseInput.value !== 'PURGE';
    });

    confirmSubmitButton.addEventListener('click', async function () {
        confirmModal.hide();
        if (!pendingAction) {
            return;
        }
        try {
            if (pendingAction.kind === 'device') {
                await postJson('/DeviceFarmUnit/RecycleBinPurgeDevice?idDevice=' + pendingAction.id, { confirmationPhrase: 'PURGE' });
                setStatus('Device marked for permanent deletion.', false);
            } else if (pendingAction.kind === 'farm') {
                await postJson('/DeviceFarmUnit/RecycleBinPurgeFarm?idDeviceFarm=' + pendingAction.id, { confirmationPhrase: 'PURGE' });
                setStatus('Farm marked for permanent deletion.', false);
            } else {
                const result = await postJson('/DeviceFarmUnit/RecycleBinEmpty', { confirmationPhrase: 'PURGE' });
                setStatus(`Recycle Bin: ${result.devicesMarked} device(s), ${result.farmsMarked} farm(s) marked for permanent deletion.`, false);
            }
            setTimeout(function () { window.location.reload(); }, 1000);
        } catch (err) {
            setStatus('Action failed: ' + err.message, true);
        }
    });
});
