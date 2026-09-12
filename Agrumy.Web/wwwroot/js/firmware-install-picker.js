// Each <option>'s value IS its manifest URL, so no board-name-to-URL mapping lives in JS. Setting
// the .manifest property (not just the attribute) is what makes esp-web-tools re-fetch it.

document.addEventListener('DOMContentLoaded', function () {
    var select = document.getElementById('flashBoardSelect');
    var installButton = document.getElementById('flashInstallButton');
    if (!select || !installButton) {
        return;
    }
    select.addEventListener('change', function () {
        installButton.manifest = select.value;
    });
});

// esp-web-tools' own install dialog (<ewt-install-dialog>) is appended straight to document.body,
// not inside our button, and is removed the moment the user closes it - without this, the page
// looks identical to before flashing the instant that popup goes away, with nothing to remind the
// admin anything happened. The dialog's "closed" event bubbles AND is composed (crosses its shadow
// root) - the library's own connect.js relies on that same event to close the serial port - so a
// document-level listener catches it without patching the vendored library. There is no public
// success/failure signal on this event (only an internal, unexposed flash-progress state), so the
// banner is worded as "session ended", not "succeeded" - it would be misleading to claim more than
// this can actually tell.
document.addEventListener('closed', function (ev) {
    if (!ev.target || ev.target.tagName !== 'EWT-INSTALL-DIALOG') {
        return;
    }
    var banner = document.getElementById('flashResultBanner');
    var text = document.getElementById('flashResultText');
    if (!banner || !text) {
        return;
    }
    text.textContent = 'Flash session ended at ' + new Date().toLocaleTimeString() +
        ' - reconnect the device and check its reported firmware version to confirm the install succeeded.';
    banner.hidden = false;
    banner.classList.add('d-flex');
});

// This page's CSP has no 'unsafe-inline' for script-src, which also governs inline event handler
// attributes - an onclick="..." on the dismiss button would be silently dropped by the browser
// (no console error unless devtools happen to be open), leaving the banner stuck with a
// dead close button. addEventListener isn't inline markup, so it isn't subject to that rule.
document.addEventListener('DOMContentLoaded', function () {
    var closeButton = document.getElementById('flashResultBannerClose');
    if (closeButton) {
        closeButton.addEventListener('click', function () {
            var banner = document.getElementById('flashResultBanner');
            banner.hidden = true;
            banner.classList.remove('d-flex');
        });
    }
});

// esp-web-tools' own "board not supported" error screen (install-dialog.ts's _renderInstall,
// ERROR branch) offers only a "Back" button that re-runs Improv detection and loops forever -
// the board genuinely has no Improv support, so there's nothing to detect. That branch also
// hardcodes allowClosing=false, so there's no X either - "Back" is the only way out, and it
// doesn't lead anywhere. Detected via the same internal _installState property
// firmware-provisioning.js already reads (esp-web-tools exposes no public state API); the fix
// runs entirely from our own code, not by patching the vendored library - a capture-phase click
// listener on the same button intercepts before Lit's own bound handler fires (stopImmediatePropagation),
// then closes exactly like a real close (synthetic "closed" event - connect.js's own listener on
// this same element reacts to it just like the real thing, releasing the serial port).
(function () {
    function fixNotSupportedScreen(dialog) {
        var state = dialog._installState;
        if (!state || state.details?.error !== 'not_supported') {
            return;
        }
        var buttons = dialog.shadowRoot.querySelectorAll('[slot="actions"] ew-text-button');
        buttons.forEach(function (button) {
            if (button.dataset.agrumyFixed) {
                return;
            }
            button.dataset.agrumyFixed = 'true';
            button.textContent = 'Close';
            button.addEventListener('click', function (event) {
                event.stopImmediatePropagation();
                dialog.dispatchEvent(new CustomEvent('closed', { bubbles: true, composed: true }));
                dialog.remove();
            }, { capture: true });
        });
    }

    var shadowObserver = new MutationObserver(function () {
        var dialog = document.querySelector('ewt-install-dialog');
        if (dialog && dialog.shadowRoot) {
            fixNotSupportedScreen(dialog);
        }
    });

    new MutationObserver(function () {
        var dialog = document.querySelector('ewt-install-dialog');
        if (dialog && dialog.shadowRoot && !dialog._agrumyShadowObserved) {
            dialog._agrumyShadowObserved = true;
            fixNotSupportedScreen(dialog);
            shadowObserver.observe(dialog.shadowRoot, { childList: true, subtree: true });
        }
    }).observe(document.body, { childList: true });
})();
