// AJAX add/delete for the Webhook SSRF allowlist, since the Webhook tab lives inside the page's one big Server Settings <form> and can't nest a form of its own - see ServerConfigController.WebhookSsrfAllowlistAdd/Delete.
document.addEventListener('DOMContentLoaded', function () {
    const addButton = document.getElementById('webhookAllowlistAddButton');
    const patternInput = document.getElementById('webhookAllowlistPattern');
    const status = document.getElementById('webhookAllowlistStatus');
    if (!addButton || !patternInput || !status) {
        return;
    }

    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';

    function setStatus(text, isError) {
        status.textContent = text;
        status.className = isError ? 'mt-2 text-danger' : 'mt-2 text-success';
    }

    addButton.addEventListener('click', async function () {
        const entry = {
            pattern: patternInput.value.trim(),
            allowPrivateNetwork: document.getElementById('webhookAllowlistAllowPrivate').checked,
            allowInsecureHttp: document.getElementById('webhookAllowlistAllowHttp').checked,
        };
        addButton.disabled = true;
        try {
            const response = await fetch('/ServerConfig/WebhookSsrfAllowlistAdd', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
                body: JSON.stringify(entry),
            });
            if (response.ok) {
                window.location.reload();
            } else {
                setStatus((await response.text()) || 'Add failed.', true);
            }
        } catch {
            setStatus('Network error - nothing was added.', true);
        } finally {
            addButton.disabled = false;
        }
    });

    document.querySelectorAll('.webhook-allowlist-delete').forEach(function (button) {
        button.addEventListener('click', async function () {
            button.disabled = true;
            try {
                const response = await fetch('/ServerConfig/WebhookSsrfAllowlistDelete?id=' + encodeURIComponent(button.dataset.id), {
                    method: 'POST',
                    headers: { 'RequestVerificationToken': token },
                });
                if (response.ok) {
                    window.location.reload();
                } else {
                    setStatus((await response.text()) || 'Delete failed.', true);
                    button.disabled = false;
                }
            } catch {
                setStatus('Network error - nothing was deleted.', true);
                button.disabled = false;
            }
        });
    });
});
