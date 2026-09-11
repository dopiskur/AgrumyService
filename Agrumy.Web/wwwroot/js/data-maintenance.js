// Both actions dispatch a background job and return immediately (202) - this script does not poll
// for completion. Purge needs a typed-phrase confirmation; batch purge alone reclaims space (no
// separate MariaDB-only shrink step - a locking OPTIMIZE TABLE rebuild isn't worth it here).
document.addEventListener('DOMContentLoaded', function () {
    const optimizeButton = document.getElementById('dataMaintenanceOptimize');
    const purgeButton = document.getElementById('dataMaintenancePurge');
    if (!optimizeButton || !purgeButton) {
        return;
    }

    const thresholdSelect = document.getElementById('dataMaintenanceThreshold');
    const status = document.getElementById('dataMaintenanceStatus');
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';

    const confirmModalEl = document.getElementById('purgeConfirmModal');
    const confirmPhraseInput = document.getElementById('purgeConfirmPhrase');
    const confirmSubmitButton = document.getElementById('purgeConfirmSubmit');
    const confirmModal = new bootstrap.Modal(confirmModalEl);

    function setStatus(text, isError) {
        status.textContent = text;
        status.className = isError ? 'mt-2 text-danger' : 'mt-2 text-success';
    }

    async function postJson(url, body) {
        const response = await fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
            body: JSON.stringify(body),
        });
        if (!response.ok) {
            throw new Error((await response.text()) || ('HTTP ' + response.status));
        }
    }

    optimizeButton.addEventListener('click', async function () {
        optimizeButton.disabled = true;
        try {
            await postJson('/ServerConfig/DataMaintenanceOptimize', { olderThanDays: Number(thresholdSelect.value) });
            setStatus('Optimize started in the background - check back later; this page does not wait for it to finish.', false);
        } catch (err) {
            setStatus('Optimize failed to start: ' + err.message, true);
        } finally {
            optimizeButton.disabled = false;
        }
    });

    purgeButton.addEventListener('click', function () {
        confirmPhraseInput.value = '';
        confirmSubmitButton.disabled = true;
        confirmModal.show();
    });

    confirmPhraseInput.addEventListener('input', function () {
        confirmSubmitButton.disabled = confirmPhraseInput.value !== 'PURGE';
    });

    confirmSubmitButton.addEventListener('click', function () {
        confirmModal.hide();
        submitPurge();
    });

    async function submitPurge() {
        purgeButton.disabled = true;
        try {
            await postJson('/ServerConfig/DataMaintenancePurge', {
                olderThanDays: Number(thresholdSelect.value),
                confirmationPhrase: 'PURGE',
            });
            setStatus('Purge started in the background - check back later; this page does not wait for it to finish.', false);
        } catch (err) {
            setStatus('Purge failed to start: ' + err.message, true);
        } finally {
            purgeButton.disabled = false;
        }
    }

    // Same confirm-phrase pattern as Purge Old Data above, but no threshold/shrink step - this forces the recycle bin's mark+reap purge cycle to run right now instead of waiting for its own schedule.
    const purgeOrphanedButton = document.getElementById('purgeOrphanedButton');
    if (purgeOrphanedButton) {
        const orphanedStatus = document.getElementById('purgeOrphanedStatus');
        const orphanedConfirmModalEl = document.getElementById('purgeOrphanedConfirmModal');
        const orphanedConfirmPhraseInput = document.getElementById('purgeOrphanedConfirmPhrase');
        const orphanedConfirmSubmitButton = document.getElementById('purgeOrphanedConfirmSubmit');
        const orphanedConfirmModal = new bootstrap.Modal(orphanedConfirmModalEl);

        purgeOrphanedButton.addEventListener('click', function () {
            orphanedConfirmPhraseInput.value = '';
            orphanedConfirmSubmitButton.disabled = true;
            orphanedConfirmModal.show();
        });

        orphanedConfirmPhraseInput.addEventListener('input', function () {
            orphanedConfirmSubmitButton.disabled = orphanedConfirmPhraseInput.value !== 'PURGE';
        });

        orphanedConfirmSubmitButton.addEventListener('click', async function () {
            orphanedConfirmModal.hide();
            purgeOrphanedButton.disabled = true;
            try {
                await postJson('/ServerConfig/DataMaintenancePurgeOrphaned', { confirmationPhrase: 'PURGE' });
                orphanedStatus.textContent = 'Purge cycle started in the background - check back later; this page does not wait for it to finish.';
                orphanedStatus.className = 'mt-2 text-success';
            } catch (err) {
                orphanedStatus.textContent = 'Purge failed to start: ' + err.message;
                orphanedStatus.className = 'mt-2 text-danger';
            } finally {
                purgeOrphanedButton.disabled = false;
            }
        });
    }
});
