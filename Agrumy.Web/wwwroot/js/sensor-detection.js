document.addEventListener('DOMContentLoaded', function () {
    var panel = document.getElementById('sensor-detect-panel');
    var checkBtn = document.getElementById('sensor-detect-check-btn');
    var resultsBox = document.getElementById('sensor-detect-results');
    var slotMapScript = document.getElementById('sensor-detect-slot-map');
    if (!panel || !checkBtn || !resultsBox || !slotMapScript) {
        return;
    }

    var deviceId = panel.getAttribute('data-device-id');
    var slotMap = JSON.parse(slotMapScript.textContent);

    function assignToSlot(fieldId, candidateId) {
        var select = document.getElementById(fieldId);
        if (select) {
            select.value = candidateId;
        }
    }

    function renderAddress(entry) {
        var wrap = document.createElement('div');
        wrap.className = 'mb-1';

        var addressLabel = document.createElement('span');
        addressLabel.textContent = 'Found at 0x' + entry.address.toString(16).toUpperCase() + ': ';
        wrap.appendChild(addressLabel);

        entry.candidates.forEach(function (candidateId) {
            var candidate = slotMap[candidateId];
            if (!candidate) {
                return; // server reported a SensorTypeId this page's catalog doesn't know - nothing to offer assigning it to
            }
            candidate.slots.forEach(function (slot) {
                var btn = document.createElement('button');
                btn.type = 'button';
                btn.className = 'btn btn-sm btn-outline-primary me-1 mb-1';
                btn.textContent = candidate.name + ' → ' + slot.label;
                btn.addEventListener('click', function () { assignToSlot(slot.fieldId, candidateId); });
                wrap.appendChild(btn);
            });
        });

        return wrap;
    }

    async function checkResult() {
        checkBtn.disabled = true;
        var originalText = checkBtn.innerHTML;
        checkBtn.innerHTML = 'Checking...';

        try {
            var response = await fetch('/Device/SensorDetectionResult?idDevice=' + encodeURIComponent(deviceId));
            if (!response.ok) {
                resultsBox.textContent = 'Could not fetch detection result.';
                return;
            }
            var result = await response.json();
            resultsBox.innerHTML = '';
            if (!result || !result.addresses || result.addresses.length === 0) {
                resultsBox.textContent = 'No result yet - issue Detect now and wait for the device to report back.';
                return;
            }
            result.addresses.forEach(function (entry) { resultsBox.appendChild(renderAddress(entry)); });
        } catch {
            resultsBox.textContent = 'Network error - could not fetch detection result.';
        } finally {
            checkBtn.disabled = false;
            checkBtn.innerHTML = originalText;
        }
    }

    checkBtn.addEventListener('click', checkResult);
});
