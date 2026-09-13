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
