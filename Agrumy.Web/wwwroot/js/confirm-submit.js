// data-confirm replaces inline onsubmit/onclick="return confirm(...)" - CSP's script-src has no
// unsafe-inline, so inline event handler attributes are silently blocked (no console error visible
// to a casual click, the action just proceeds without ever asking). Delegated on document so it
// still works after a live-refresh/AJAX innerHTML swap replaces the element.
document.addEventListener('submit', function (e) {
    var el = e.target.closest('form[data-confirm]');
    if (el && !confirm(el.getAttribute('data-confirm'))) {
        e.preventDefault();
    }
});
document.addEventListener('click', function (e) {
    var el = e.target.closest('button[data-confirm], a[data-confirm]');
    if (el && !confirm(el.getAttribute('data-confirm'))) {
        e.preventDefault();
    }
});

// data-autosubmit replaces inline onchange="this.form.requestSubmit()" - same CSP restriction as above.
document.addEventListener('change', function (e) {
    var el = e.target.closest('[data-autosubmit]');
    if (el && el.form) {
        el.form.requestSubmit();
    }
});

// A .js-return-url hidden field replaces inline onsubmit="..." that filled it from window.location - the
// server-rendered Request.Path can be wrong when the form lives inside an AJAX-swapped fragment.
document.addEventListener('submit', function (e) {
    var field = e.target.querySelector && e.target.querySelector('.js-return-url');
    if (field) {
        field.value = window.location.pathname + window.location.search;
    }
});
