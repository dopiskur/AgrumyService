// The board is flashed first; once it actually finishes (esp-web-tools exposes no public success event, but its install-dialog element leaves its internal `_installState`/`port` as plain, still-readable properties after removal from the DOM) the WiFi step opens, and submitting it sends the credentials over that same reopened SerialPort, then this listens on it for the device's own confirmation once it registers - see AgrumyFirmware's DeviceController::tryReadSerialProvisioning and the "agrumyRegistered" echo at the end of registerDevice().

(function () {
    var PROVISION_SEND_TYPE = 'agrumyProvision';
    var REGISTERED_ECHO_TYPE = 'agrumyRegistered';
    // Generous - covers WiFi association, DHCP, TLS handshake and the server round trip, not just the write itself.
    var REGISTRATION_WAIT_MS = 60000;

    var wifiStep, wifiSelect, wifiSsidInput, wifiPasswordInput, wifiSavedList, continueButton, flashStep;
    var statusEl, assignStep, farmSelect, unitSelect, zoneSelect, assignButton, skipButton;
    var farmTree = null;
    var provisionedDeviceId = null;
    var pendingPort = null;

    function antiForgeryToken() {
        return document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';
    }

    function setStatus(text) {
        if (statusEl) {
            statusEl.textContent = text;
        }
    }

    async function postForm(url, params) {
        var body = Object.keys(params).map(function (k) { return k + '=' + encodeURIComponent(params[k]); }).join('&');
        var response = await fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'RequestVerificationToken': antiForgeryToken() },
            body: body,
        });
        if (!response.ok) {
            throw new Error(await response.text().catch(function () { return response.statusText; }));
        }
        var contentType = response.headers.get('content-type') || '';
        return contentType.indexOf('application/json') !== -1 ? response.json() : null;
    }

    async function loadWifiOptions() {
        try {
            var configs = await (await fetch('/Firmware/WifiConfigsForProvisioning')).json();
            configs.forEach(function (c) {
                var option = document.createElement('option');
                option.value = String(c.id);
                option.textContent = c.ssid;
                wifiSelect.appendChild(option);

                var item = document.createElement('button');
                item.type = 'button';
                item.className = 'list-group-item list-group-item-action py-1';
                item.textContent = c.ssid;
                item.addEventListener('click', function () {
                    wifiSelect.value = String(c.id);
                    toggleWifiManualFields();
                    wifiSavedList.querySelectorAll('.active').forEach(function (el) { el.classList.remove('active'); });
                    item.classList.add('active');
                });
                wifiSavedList.appendChild(item);
            });
        } catch (err) {
            // Non-fatal - manual entry still works without the saved-network list.
        }
    }

    function toggleWifiManualFields() {
        wifiStep.querySelector('#provisionWifiManual').hidden = !!wifiSelect.value;
    }

    async function resolveWifiCredentials() {
        if (wifiSelect.value) {
            var resolved = await postForm('/Firmware/ResolveWifiSecret', { idTenantWifiConfig: wifiSelect.value });
            return { ssid: resolved.ssid, password: resolved.password };
        }
        return { ssid: wifiSsidInput.value.trim(), password: wifiPasswordInput.value };
    }

    // A '\n'-delimited line reader over the port's own ReadableStream - buffers across reads since Web Serial hands back arbitrary chunk boundaries, not whole lines.
    function makeLineReader(port) {
        var reader = port.readable.getReader();
        var decoder = new TextDecoder();
        var buffer = '';
        return {
            async nextLine(deadline) {
                while (true) {
                    var newlineIndex = buffer.indexOf('\n');
                    if (newlineIndex !== -1) {
                        var line = buffer.slice(0, newlineIndex);
                        buffer = buffer.slice(newlineIndex + 1);
                        return line.trim();
                    }
                    var remaining = deadline - Date.now();
                    if (remaining <= 0) {
                        return null;
                    }
                    var timeout = new Promise(function (resolve) { setTimeout(function () { resolve({ timedOut: true }); }, remaining); });
                    var result = await Promise.race([reader.read(), timeout]);
                    if (result.timedOut) {
                        return null;
                    }
                    if (result.done) {
                        return null;
                    }
                    buffer += decoder.decode(result.value, { stream: true });
                }
            },
            release() {
                try { reader.releaseLock(); } catch (e) { /* already released */ }
            },
        };
    }

    async function sendProvisioning(port, credentials) {
        var email = '', devicePin = '', servicePoint = '';
        try {
            var mine = await (await fetch('/Firmware/MyProvisioningCredentials')).json();
            email = mine.email || '';
            devicePin = mine.devicePin || '';
            servicePoint = mine.servicePoint || '';
        } catch (err) {
            // Falls through with blanks - the device will still save WiFi, just without a usable login/PIN/servicePoint; registerDevice() reports 401 and retries (or targets the wrong host), no worse than not provisioning at all.
        }

        var payload = {
            type: PROVISION_SEND_TYPE,
            wifiSsid: credentials.ssid,
            wifiPassword: credentials.password,
            userLogin: email,
            devicePin: devicePin,
            servicePoint: servicePoint,
            displayName: '',
        };

        await port.open({ baudRate: 115200, bufferSize: 8192 });
        var writer = port.writable.getWriter();
        try {
            await writer.write(new TextEncoder().encode(JSON.stringify(payload) + '\n'));
        } finally {
            writer.releaseLock();
        }

        var lineReader = makeLineReader(port);
        var deadline = Date.now() + REGISTRATION_WAIT_MS;
        var deviceId = null;
        try {
            while (true) {
                var line = await lineReader.nextLine(deadline);
                if (line === null) {
                    break; // timed out or port closed
                }
                if (!line) {
                    continue;
                }
                try {
                    var parsed = JSON.parse(line);
                    if (parsed && parsed.type === REGISTERED_ECHO_TYPE && typeof parsed.deviceId === 'number') {
                        deviceId = parsed.deviceId;
                        break;
                    }
                } catch (e) {
                    // Ordinary boot log line, not JSON - expected, keep reading.
                }
            }
        } finally {
            lineReader.release();
            try { await port.close(); } catch (e) { /* already closed/disconnected */ }
        }
        return deviceId;
    }

    function unitNodeHtml(select, node) {
        var option = document.createElement('option');
        option.value = String(node.id);
        option.textContent = node.name;
        select.appendChild(option);
    }

    function populateZones(zones) {
        zoneSelect.innerHTML = '';
        zones.forEach(function (z) { unitNodeHtml(zoneSelect, z); });
    }

    function populateUnits(units) {
        unitSelect.innerHTML = '';
        units.forEach(function (u) { unitNodeHtml(unitSelect, u); });
        if (units.length > 0) {
            populateZones(units[0].zones);
        }
    }

    async function loadAssignPicker() {
        farmTree = await (await fetch('/Firmware/FarmTree')).json();
        var farms = farmTree.farms || [];
        var loneUnits = (farmTree.unassignedUnits || []);

        if (farms.length <= 1) {
            // One farm (or none at all) is implicit - same "no farm-object" principle as the Farms page itself; skip straight to Unit > Zone.
            farmSelect.hidden = true;
            unitSelect.hidden = false;
            var units = farms.length === 1 ? farms[0].units.concat(loneUnits) : loneUnits;
            populateUnits(units);
        } else {
            farmSelect.hidden = false;
            unitSelect.hidden = false;
            farmSelect.innerHTML = '';
            farms.forEach(function (f) { unitNodeHtml(farmSelect, f); });
            populateUnits(farms[0].units);
        }
    }

    function findFarm(id) {
        return (farmTree.farms || []).find(function (f) { return String(f.id) === String(id); });
    }

    function findUnit(units, id) {
        return units.find(function (u) { return String(u.id) === String(id); });
    }

    function wireAssignPicker() {
        farmSelect.addEventListener('change', function () {
            var farm = findFarm(farmSelect.value);
            populateUnits(farm ? farm.units : []);
        });
        unitSelect.addEventListener('change', function () {
            var units = farmSelect.hidden ? (farmTree.farms.length === 1 ? farmTree.farms[0].units.concat(farmTree.unassignedUnits) : farmTree.unassignedUnits)
                : (findFarm(farmSelect.value)?.units || []);
            var unit = findUnit(units, unitSelect.value);
            populateZones(unit ? unit.zones : []);
        });
    }

    async function onContinueClick() {
        continueButton.disabled = true;
        try {
            var credentials = await resolveWifiCredentials();
            if (!credentials.ssid) {
                setStatus('Enter a WiFi network first.');
                continueButton.disabled = false;
                return;
            }
            if (!pendingPort) {
                setStatus('No flashed device to provision - flash a board first.');
                continueButton.disabled = false;
                return;
            }
            setStatus('Sending WiFi and login to the device…');
            var deviceId = await sendProvisioning(pendingPort, credentials);
            if (deviceId === null) {
                setStatus('No confirmation from the device - it may still be trying in the background, or it fell through to its own Agrumy_<mac> WiFi setup network. Check the Fleet page shortly.');
                continueButton.disabled = false;
                return;
            }
            provisionedDeviceId = deviceId;
            setStatus('Device registered (#' + deviceId + ').');
            wifiStep.hidden = true;
            assignStep.hidden = false;
            wireAssignPicker();
            await loadAssignPicker();
        } catch (err) {
            setStatus('Provisioning failed: ' + err.message);
            continueButton.disabled = false;
        }
    }

    async function onFlashClosed(ev) {
        if (!ev.target || ev.target.tagName !== 'EWT-INSTALL-DIALOG') {
            return;
        }
        var installState = ev.target._installState;
        if (!installState || installState.state !== 'finished') {
            return; // cancelled, errored, or never got far enough - nothing to provision
        }
        var port = ev.target.port;
        if (!port) {
            return;
        }
        pendingPort = port;
        flashStep.hidden = true;
        wifiStep.hidden = false;
        setStatus('Flash complete - enter WiFi credentials to finish provisioning.');
    }

    document.addEventListener('DOMContentLoaded', function () {
        wifiStep = document.getElementById('provisionWifiStep');
        wifiSelect = document.getElementById('provisionWifiSelect');
        wifiSsidInput = document.getElementById('provisionWifiSsid');
        wifiPasswordInput = document.getElementById('provisionWifiPassword');
        wifiSavedList = document.getElementById('provisionWifiSavedList');
        continueButton = document.getElementById('provisionContinueButton');
        flashStep = document.getElementById('provisionFlashStep');
        statusEl = document.getElementById('provisionStatus');
        assignStep = document.getElementById('provisionAssignStep');
        farmSelect = document.getElementById('provisionFarmSelect');
        unitSelect = document.getElementById('provisionUnitSelect');
        zoneSelect = document.getElementById('provisionZoneSelect');
        assignButton = document.getElementById('provisionAssignButton');
        skipButton = document.getElementById('provisionSkipButton');
        if (!wifiStep || !continueButton || !flashStep) {
            return;
        }

        wifiSelect.addEventListener('change', toggleWifiManualFields);
        loadWifiOptions();

        continueButton.addEventListener('click', function () {
            onContinueClick().catch(function () { /* onContinueClick already reports its own errors via setStatus */ });
        });

        assignButton.addEventListener('click', async function () {
            assignButton.disabled = true;
            skipButton.disabled = true;
            try {
                await postForm('/Firmware/AssignProvisionedDevice', { idDevice: provisionedDeviceId, idDeviceFarmUnitZone: zoneSelect.value });
                setStatus('Assigned. Find it on the Fleet page.');
                assignStep.hidden = true;
            } catch (err) {
                setStatus('Assign failed: ' + err.message);
                assignButton.disabled = false;
                skipButton.disabled = false;
            }
        });

        skipButton.addEventListener('click', function () {
            setStatus('Added to the fleet, unassigned. Find it on the Fleet page.');
            assignStep.hidden = true;
        });
    });

    document.addEventListener('closed', function (ev) {
        onFlashClosed(ev).catch(function () { /* best-effort - the static flashResultBanner still covers the fallback case */ });
    });
})();
