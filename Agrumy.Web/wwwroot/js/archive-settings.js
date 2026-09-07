// Roadmap #209 - Data Archiving is its own self-contained widget, saved independently of the page's
// main Save button (test-before-persist, unlike every other Server Settings field) - see
// ServerConfigController.SaveArchiveSettings/TestArchiveDatabase.
document.addEventListener('DOMContentLoaded', function () {
    const enabledToggle = document.getElementById('archiveEnabledToggle');
    const configSection = document.getElementById('archiveConfigSection');
    if (!enabledToggle || !configSection) {
        return;
    }

    const disabledSaveRow = document.getElementById('archiveDisabledSaveRow');
    const cutoffModeSelect = document.getElementById('archiveCutoffModeSelect');
    const customDateField = document.getElementById('archiveCustomDateField');
    const rollingDaysField = document.getElementById('archiveRollingDaysField');
    const customDateInput = document.getElementById('archiveCustomDateInput');
    const rollingDaysSelect = document.getElementById('archiveRollingDaysSelect');
    const configuredBlock = document.getElementById('archiveCredentialsConfigured');
    const formBlock = document.getElementById('archiveCredentialsForm');
    const changeButton = document.getElementById('archiveChangeButton');
    const cancelButton = document.getElementById('archiveCancelButton');
    const testButton = document.getElementById('archiveTestButton');
    const saveButton = document.getElementById('archiveSaveButton');
    const saveDisabledButton = document.getElementById('archiveSaveDisabledButton');
    const status = document.getElementById('archiveStatus');
    const hostInput = document.getElementById('archiveHostInput');
    const portInput = document.getElementById('archivePortInput');
    const databaseNameInput = document.getElementById('archiveDatabaseNameInput');
    const usernameInput = document.getElementById('archiveUsernameInput');
    const passwordInput = document.getElementById('archivePasswordInput');
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';

    function setStatus(text, isError) {
        status.textContent = text;
        status.className = isError ? 'mt-2 text-danger' : 'mt-2 text-success';
    }

    function updateEnabledVisibility() {
        const enabled = enabledToggle.checked;
        configSection.style.display = enabled ? 'block' : 'none';
        disabledSaveRow.style.display = enabled ? 'none' : 'block';
    }
    enabledToggle.addEventListener('change', updateEnabledVisibility);

    function updateCutoffModeVisibility() {
        const mode = cutoffModeSelect.value;
        customDateField.style.display = mode === '1' ? 'block' : 'none';
        rollingDaysField.style.display = mode === '2' ? 'block' : 'none';
    }
    cutoffModeSelect?.addEventListener('change', updateCutoffModeVisibility);

    changeButton?.addEventListener('click', function () {
        configuredBlock.style.display = 'none';
        formBlock.style.display = 'block';
        status.textContent = '';
    });
    cancelButton?.addEventListener('click', function () {
        formBlock.style.display = 'none';
        configuredBlock.style.display = 'block';
        status.textContent = '';
    });

    function buildCredentialsPayload() {
        return {
            host: hostInput.value.trim(),
            port: parseInt(portInput.value, 10) || null,
            databaseName: databaseNameInput.value.trim(),
            username: usernameInput.value.trim(),
            password: passwordInput.value,
        };
    }

    function validateCredentials(payload) {
        if (!payload.host || !payload.databaseName || !payload.username) {
            setStatus('Host, database name, and username are required.', true);
            return false;
        }
        return true;
    }

    testButton?.addEventListener('click', async function () {
        const payload = buildCredentialsPayload();
        if (!validateCredentials(payload)) {
            return;
        }

        testButton.disabled = true;
        setStatus('Testing connection...', false);
        try {
            const response = await fetch('/ServerConfig/TestArchiveDatabase', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
                body: JSON.stringify(payload),
            });
            if (response.ok) {
                setStatus('Connection OK.', false);
            } else {
                setStatus((await response.text()) || 'Connection test failed.', true);
            }
        } catch {
            setStatus('Network error - could not test the connection.', true);
        } finally {
            testButton.disabled = false;
        }
    });

    async function saveArchiveSettings(enabled) {
        const request = enabled
            ? {
                enabled: true,
                cutoffMode: parseInt(cutoffModeSelect.value, 10),
                customCutoffDate: cutoffModeSelect.value === '1' ? (customDateInput.value || null) : null,
                customRollingDays: cutoffModeSelect.value === '2' ? parseInt(rollingDaysSelect.value, 10) : null,
                ...buildCredentialsPayload(),
            }
            : { enabled: false, cutoffMode: 0 };

        if (enabled && !validateCredentials(request)) {
            return;
        }

        setStatus('Saving...', false);
        try {
            const response = await fetch('/ServerConfig/SaveArchiveSettings', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
                body: JSON.stringify(request),
            });
            if (response.ok) {
                setStatus('Saved.', false);
                setTimeout(() => window.location.reload(), 600);
            } else {
                setStatus((await response.text()) || 'Save failed.', true);
            }
        } catch {
            setStatus('Network error - nothing was saved.', true);
        }
    }

    saveButton?.addEventListener('click', function () {
        saveButton.disabled = true;
        saveArchiveSettings(true).finally(() => { saveButton.disabled = false; });
    });
    saveDisabledButton?.addEventListener('click', function () {
        saveDisabledButton.disabled = true;
        saveArchiveSettings(false).finally(() => { saveDisabledButton.disabled = false; });
    });
});
