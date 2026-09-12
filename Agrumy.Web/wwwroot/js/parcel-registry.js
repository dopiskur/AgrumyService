// Ready-for-season toggle on the Parcels registry - turning ON always asks whether to populate prep
// dates first (Yes takes the admin to the Parcel page's existing dnevnik "Add event" form, Skip just
// confirms ready with no dates); turning OFF (reverting) needs no dialog, it just reverts.
function initParcelReadyToggles(options) {
    const modalEl = document.getElementById('readyForSeasonModal');
    if (!modalEl) {
        return;
    }
    const modal = new bootstrap.Modal(modalEl);
    const yesButton = document.getElementById('readyForSeasonYes');
    const skipButton = document.getElementById('readyForSeasonSkip');
    let pendingToggle = null;

    async function setReady(idFarmParcelZone, ready) {
        return fetch(options.setUrl, {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'RequestVerificationToken': options.antiForgeryToken },
            body: 'idFarmParcelZone=' + encodeURIComponent(idFarmParcelZone) + '&ready=' + ready,
        });
    }

    function updateBadge(toggle, ready) {
        const badge = toggle.closest('.form-check')?.querySelector('.ready-badge');
        if (badge) {
            badge.textContent = ready ? 'Ready' : 'Not ready';
            badge.classList.toggle('text-bg-success', ready);
            badge.classList.toggle('text-bg-danger', !ready);
        }
    }

    document.querySelectorAll('.parcel-ready-toggle').forEach(function (toggle) {
        toggle.addEventListener('change', async function () {
            const idFarmParcelZone = toggle.dataset.idFarmParcelZone;
            if (!toggle.checked) {
                // Reverting to Not-ready - no dialog, just apply it.
                const response = await setReady(idFarmParcelZone, false);
                if (response.ok) {
                    updateBadge(toggle, false);
                } else {
                    toggle.checked = true;
                }
                return;
            }

            // Turning Ready ON - hold off applying until the dialog resolves; revert the visual state meanwhile.
            toggle.checked = false;
            pendingToggle = toggle;
            modal.show();
        });
    });

    async function confirmReady() {
        if (!pendingToggle) {
            return;
        }
        const idFarmParcelZone = pendingToggle.dataset.idFarmParcelZone;
        const response = await setReady(idFarmParcelZone, true);
        if (response.ok) {
            pendingToggle.checked = true;
            updateBadge(pendingToggle, true);
        }
    }

    skipButton.addEventListener('click', async function () {
        await confirmReady();
        modal.hide();
        pendingToggle = null;
    });

    yesButton.addEventListener('click', async function () {
        const idFarmParcelZone = pendingToggle?.dataset.idFarmParcelZone;
        await confirmReady();
        if (idFarmParcelZone) {
            window.location.href = options.parcelDetailsUrlTemplate.replace('__ID__', idFarmParcelZone);
        }
    });

    modalEl.addEventListener('hidden.bs.modal', function () {
        pendingToggle = null;
    });
}
