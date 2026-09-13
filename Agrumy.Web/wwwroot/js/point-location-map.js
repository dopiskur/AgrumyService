// One draggable-marker Leaflet map per .point-location-map element - click anywhere to
// place/move the pin, updates the two hidden lat/lon inputs named by data-lat-input/
// data-lon-input. Same "init on first shown.bs.modal, not on page load" convention as
// parcel-geometry-map.js, since Leaflet can't measure a display:none container.
document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('.point-location-map').forEach(function (mapEl) {
        const modal = mapEl.closest('.modal');
        if (modal) {
            modal.addEventListener('shown.bs.modal', function () { initPointLocationMap(mapEl); }, { once: true });
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
        // Same-origin passthrough (Agrumy.Web/Controllers/View/MapController.cs) to Agrumy.Api's TileProxy - see parcel-geometry-map.js's own remark on why a plain <img> tile request can't be used directly.
        L.tileLayer('/Map/Tile/{z}/{x}/{y}.png', {
            maxZoom: 19,
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
        }).addTo(map);

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
