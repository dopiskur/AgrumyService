// One map instance per _SatelliteMap partial (Detaljni dizajn S, sesija C) - scope only changes which
// zones the server returns, the client-side rendering is identical across Farm/Sowing/Parcel/Zone tabs.
const SATELLITE_INDEX_NAMES = ['', 'Ndvi', 'Ndmi', 'Ndwi', 'Ndsi', 'SwirComposite', 'NaturalColor'];
const SATELLITE_LEGENDS = {
    Ndvi: 'NDVI: brown/tan (bare or stressed) -> yellow -> green (dense canopy), scale -1..1.',
    Ndmi: 'NDMI: tan (dry) -> blue (high moisture), scale -1..1.',
    Ndwi: 'NDWI: green (land) -> white -> blue (open water), scale -1..1.',
    Ndsi: 'NDSI: grey (no snow) -> cyan/white (snow or ice), scale -1..1.',
    SwirComposite: 'SWIR false-color composite - no fixed palette (visual only).',
    NaturalColor: 'Natural color composite - no fixed palette (visual only).',
};

function initSatelliteMap(mapId) {
    const mapEl = document.getElementById(mapId);
    if (!mapEl || typeof L === 'undefined') {
        return;
    }
    const root = mapEl.closest('.card').querySelector('[data-sat-role="root"]');
    const scope = root.getAttribute('data-scope');
    const id = root.getAttribute('data-id');
    const canManage = root.getAttribute('data-can-manage') === '1';
    const card = mapEl.closest('.card');
    const indexSelect = card.querySelector('[data-sat-role="index"]');
    const reliableCheckbox = card.querySelector('[data-sat-role="onlyReliable"]');
    const slider = card.querySelector('[data-sat-role="dateSlider"]');
    const dateLabel = card.querySelector('[data-sat-role="dateLabel"]');
    const legendEl = card.querySelector('[data-sat-role="legend"]');
    const emptyNotice = card.querySelector('[data-sat-role="emptyNotice"]');
    const syncButton = card.querySelector('[data-sat-role="syncNow"]');

    const map = L.map(mapEl);
    // Same-origin passthrough (Agrumy.Web/Controllers/View/MapController.cs) to Agrumy.Api's TileProxy - a plain <img> tile request can't carry the JWT that TileProxy's [Authorize] requires.
    L.tileLayer(`/Map/Tile/{z}/{x}/{y}.png`, {
        maxZoom: 19,
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
    }).addTo(map);
    map.setView([45.815, 15.982], 13);

    let overlayLayer = L.layerGroup().addTo(map);
    let availableDates = [];

    function updateLegend() {
        const indexName = SATELLITE_INDEX_NAMES[parseInt(indexSelect.value, 10)];
        legendEl.textContent = SATELLITE_LEGENDS[indexName] || '';
    }

    async function loadDates() {
        const response = await fetch(`/FarmOpenfield/SatelliteMapDates?scope=${scope}&id=${id}`);
        availableDates = response.ok ? await response.json() : [];
        if (availableDates.length === 0) {
            slider.disabled = true;
            slider.max = 0;
            dateLabel.textContent = 'No dates yet';
            emptyNotice.hidden = false;
            return false;
        }
        emptyNotice.hidden = true;
        slider.disabled = false;
        slider.max = availableDates.length - 1;
        slider.value = availableDates.length - 1; // default to the most recent date
        dateLabel.textContent = availableDates[availableDates.length - 1];
        return true;
    }

    async function loadAndRender() {
        const hasDates = availableDates.length > 0;
        const selectedDate = hasDates ? availableDates[parseInt(slider.value, 10)] : null;
        dateLabel.textContent = selectedDate || 'No dates yet';
        const indexValue = indexSelect.value;

        let url = `/FarmOpenfield/SatelliteMap?scope=${scope}&id=${id}&index=${indexValue}`;
        if (selectedDate) {
            url += `&date=${selectedDate}`;
        }
        const response = await fetch(url);
        if (!response.ok) {
            return;
        }
        const data = await response.json();
        const onlyReliable = reliableCheckbox.checked;

        overlayLayer.clearLayers();
        const bounds = L.latLngBounds([]);

        // Parcel outlines first (thicker) so zone outlines draw on top.
        (data.parcels || []).forEach((p) => {
            if (!p.geometryGeoJson) {
                return;
            }
            try {
                const layer = L.geoJSON(JSON.parse(p.geometryGeoJson), { style: { color: '#212529', weight: 3, fillOpacity: 0 } });
                layer.addTo(overlayLayer);
                bounds.extend(layer.getBounds());
            } catch (e) { /* malformed geometry - skip this parcel's outline, the zones still render */ }
        });

        (data.zones || []).forEach((z) => {
            if (!z.geometryGeoJson) {
                return;
            }
            let zoneBounds = null;
            try {
                const outline = L.geoJSON(JSON.parse(z.geometryGeoJson), { style: { color: '#495057', weight: 1, fillOpacity: 0 } });
                outline.bindTooltip(z.zoneName || ('Zone ' + z.zoneId));
                outline.addTo(overlayLayer);
                zoneBounds = outline.getBounds();
                bounds.extend(zoneBounds);
            } catch (e) { return; }

            const showRaster = z.hasData && z.sceneId && (!onlyReliable || z.reliable);
            if (showRaster && zoneBounds) {
                const opacity = z.reliable ? 1 : 0.5; // unreliable (below MinValidPixelPercent) still shown, faded rather than hidden - D4's "never disappears"
                // Same-origin passthrough (FarmOpenfieldController.SatelliteImage), not a direct Agrumy.Api link - a plain <img> can't carry the JWT Agrumy.Api requires.
                const url = `/FarmOpenfield/SatelliteImage?idFarmParcelZone=${z.zoneId}&idScene=${z.sceneId}&index=${indexValue}`;
                L.imageOverlay(url, zoneBounds, { opacity }).addTo(overlayLayer);
            }
        });

        if (bounds.isValid()) {
            map.fitBounds(bounds, { padding: [20, 20] });
        }
    }

    indexSelect.addEventListener('change', () => { updateLegend(); loadAndRender(); });
    reliableCheckbox.addEventListener('change', loadAndRender);
    slider.addEventListener('input', loadAndRender);

    if (syncButton) {
        syncButton.addEventListener('click', async () => {
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';
            syncButton.disabled = true;
            try {
                const response = await fetch(`/FarmOpenfield/SatelliteSyncNow?scope=${scope}&id=${id}`, {
                    method: 'POST',
                    headers: { 'RequestVerificationToken': token },
                });
                syncButton.textContent = response.ok ? 'Sync requested' : 'Sync failed';
            } catch (e) {
                syncButton.textContent = 'Sync failed';
            }
        });
    }

    updateLegend();
    loadDates().then(loadAndRender);

    // The card may start on a hidden Bootstrap tab - Leaflet measured a zero-size container at init otherwise.
    document.querySelectorAll(`[data-bs-target]`).forEach((tabButton) => {
        tabButton.addEventListener('shown.bs.tab', () => map.invalidateSize());
    });
}
