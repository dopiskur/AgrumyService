// Delegated on document (not per-row) so a live-refresh tbody swap never leaves stale/missing
// listeners - same reasoning as device-commands.js. A no-op on any page with no such toggle.
document.addEventListener('change', async function (e) {
    var el = e.target.closest('[data-fleet-enabled-toggle]');
    if (!el) {
        return;
    }
    var deviceId = el.dataset.deviceId;
    var enabled = el.checked;
    el.disabled = true;

    var token = document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';
    var params = new URLSearchParams({ idDevice: deviceId, enabled: enabled });
    try {
        var response = await fetch('/Device/SetEnabled?' + params.toString(), {
            method: 'POST',
            headers: { 'RequestVerificationToken': token },
        });
        if (response.ok) {
            // The Status badge in the same row also depends on Enabled - pull a fresh copy of every
            // row rather than hand-patching just this one cell's badge markup here.
            if (typeof window.onFleetRowsChanged === 'function') {
                window.onFleetRowsChanged();
            }
        } else {
            el.checked = !enabled;
            alert((await response.text()) || 'Failed to update.');
        }
    } catch {
        el.checked = !enabled;
        alert('Network error - change not saved.');
    } finally {
        el.disabled = false;
    }
});
