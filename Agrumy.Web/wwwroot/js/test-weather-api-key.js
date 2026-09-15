// Tests through the SAVED OpenWeatherMap key/location (Save first, then test) - see ServerConfigController.TestWeatherApiKey.
// On success, reloads so the server-rendered badge next to the key field picks up the fresh WeatherApiKeyValidatedUtc.
document.addEventListener('DOMContentLoaded', function () {
    const button = document.getElementById('testWeatherApiKeyButton');
    const status = document.getElementById('testWeatherApiKeyStatus');
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
        setStatus('Testing...', false);
        try {
            const response = await fetch('/ServerConfig/TestWeatherApiKey', {
                method: 'POST',
                headers: { 'RequestVerificationToken': token },
            });
            if (response.ok) {
                setStatus('Key is valid.', false);
                setTimeout(() => window.location.reload(), 600);
            } else {
                setStatus((await response.text()) || 'Key test failed.', true);
                button.disabled = false;
            }
        } catch {
            setStatus('Network error - could not test the key.', true);
            button.disabled = false;
        }
    });
});
