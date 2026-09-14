// One draggable-marker Leaflet map per .point-location-map element - click anywhere to
// place/move the pin, updates the two hidden lat/lon inputs named by data-lat-input/
// data-lon-input. Same "init on first shown, not on page load" convention as
// parcel-geometry-map.js, since Leaflet can't measure a display:none container - covers both
// a modal (shown.bs.modal) and a Bootstrap tab pane (shown.bs.tab on its own tab button).
document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('.point-location-map').forEach(function (mapEl) {
        const modal = mapEl.closest('.modal');
        const tabPane = mapEl.closest('.tab-pane');
        const tabButton = tabPane && !tabPane.classList.contains('active')
            ? document.querySelector('button[data-bs-target="#' + tabPane.id + '"]')
            : null;
        if (modal) {
            modal.addEventListener('shown.bs.modal', function () { initPointLocationMap(mapEl); }, { once: true });
        } else if (tabButton) {
            tabButton.addEventListener('shown.bs.tab', function () { initPointLocationMap(mapEl); }, { once: true });
        } else {
            initPointLocationMap(mapEl);
        }
    });

    function initPointLocationMap(mapEl) {
        if (typeof L === 'undefined') {
            return;
        }
        const DEFAULT_LAT = 45.815;
        const DEFAULT_LON = 15.982;

        // parseFloat('') is NaN, not null/undefined, so plain `??` chaining wouldn't fall through an unset attribute - this treats any unparsable value as absent.
        function parsedOrNull(value) {
            const n = parseFloat(value);
            return Number.isFinite(n) ? n : null;
        }

        const latInput = document.getElementById(mapEl.dataset.latInput);
        const lonInput = document.getElementById(mapEl.dataset.lonInput);
        const initialLat = parsedOrNull(latInput?.value);
        const initialLon = parsedOrNull(lonInput?.value);
        // Falls back to the complex's own pin (data-center-lat/lon) when this unit has none yet, so placing a unit starts centered on its greenhouse complex rather than the whole country.
        const centerLat = initialLat ?? parsedOrNull(mapEl.dataset.centerLat) ?? DEFAULT_LAT;
        const centerLon = initialLon ?? parsedOrNull(mapEl.dataset.centerLon) ?? DEFAULT_LON;

        const map = L.map(mapEl).setView([centerLat, centerLon], initialLat != null ? 17 : 14);
        // Bootstrap's shown.bs.tab/shown.bs.modal fires as soon as the fade class flips, which can be a tick before the container's layout box actually settles to its final size - Leaflet caches the size it measured at construction, so a stale (usually zero-height) reading here shows as a broken, partially-tiled map until the window is later resized. One extra measurement next tick is the standard fix.
        setTimeout(() => map.invalidateSize(), 0);
        // Same-origin passthrough (Agrumy.Web/Controllers/View/MapController.cs) to Agrumy.Api's TileProxy - see parcel-geometry-map.js's own remark on why a plain <img> tile request can't be used directly.
        const osmLayer = L.tileLayer('/Map/Tile/{z}/{x}/{y}.png', {
            maxZoom: 19,
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
        }).addTo(map);
        // Same satellite source as parcel-geometry-map.js's "ARKOD karta" base layer, minus the ARKOD boundary overlay - a plain aerial view is all a point pin needs.
        const satelliteLayer = L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}', {
            maxZoom: 19,
            attribution: 'Tiles &copy; Esri',
        });
        L.control.layers({ 'OpenStreetMap': osmLayer, 'Satellite': satelliteLayer }).addTo(map);

        function setCoords(latlng) {
            if (latInput) { latInput.value = latlng.lat.toFixed(6); }
            if (lonInput) { lonInput.value = latlng.lng.toFixed(6); }
        }

        let marker = initialLat != null && initialLon != null ? L.marker([initialLat, initialLon], { draggable: true }) : null;
        if (marker) {
            marker.addTo(map).on('dragend', () => setCoords(marker.getLatLng()));
        }

        map.on('click', (e) => {
            if (marker) {
                marker.setLatLng(e.latlng);
            } else {
                marker = L.marker(e.latlng, { draggable: true }).addTo(map).on('dragend', () => setCoords(marker.getLatLng()));
            }
            setCoords(e.latlng);
        });

        const clearBtn = document.getElementById(mapEl.dataset.clearButton);
        if (clearBtn) {
            clearBtn.addEventListener('click', () => {
                if (marker) {
                    map.removeLayer(marker);
                    marker = null;
                }
                if (latInput) { latInput.value = ''; }
                if (lonInput) { lonInput.value = ''; }
            });
        }
    }
});
