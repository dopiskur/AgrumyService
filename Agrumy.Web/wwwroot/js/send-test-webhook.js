// Sends through the SAVED Webhook settings (Save first, then test) - see ServerConfigController.TestWebhook.
document.addEventListener('DOMContentLoaded', function () {
    const button = document.getElementById('testWebhookButton');
    const status = document.getElementById('testWebhookStatus');
    if (!button || !status) {
        return;
    }

    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';

    function setStatus(text, isError) {
        status.textContent = text;
        status.className = isError ? 'mt-2 text-danger' : 'mt-2 text-success';
    }

    button.addEventListener('click', async function () {
        button.disabled = true;
        setStatus('Sending...', false);
        try {
            const response = await fetch('/ServerConfig/TestWebhook', {
                method: 'POST',
                headers: { 'RequestVerificationToken': token },
            });
            if (response.ok) {
                setStatus('Sent - check the endpoint received it.', false);
            } else {
                setStatus((await response.text()) || 'Send failed.', true);
            }
        } catch {
            setStatus('Network error - test webhook not sent.', true);
        } finally {
            button.disabled = false;
        }
    });
});
