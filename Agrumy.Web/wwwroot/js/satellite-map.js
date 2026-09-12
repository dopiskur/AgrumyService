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

// Same gradient stops as SatellitePaletteRenderer.PaletteFor (Agrumy.Api) - keep the two in sync, this is the client-side half of the same palette.
const SATELLITE_PALETTE_STOPS = {
    Ndvi: [[140, 81, 10], [223, 194, 125], [247, 247, 247], [161, 215, 106], [0, 109, 44]],
    Ndmi: [[166, 97, 26], [223, 194, 125], [245, 245, 245], [128, 205, 193], [1, 102, 94]],
    Ndwi: [[1, 102, 94], [128, 205, 193], [245, 245, 245], [140, 81, 10], [84, 48, 5]],
    Ndsi: [[82, 82, 82], [189, 189, 189], [240, 240, 240], [158, 202, 225], [8, 81, 156]],
};

const paletteTableCache = {};
// Mirrors SatellitePaletteRenderer.BuildGradient exactly (256 steps) so a given byte value maps to the same color server and client side.
function paletteTableFor(indexName) {
    if (paletteTableCache[indexName]) {
        return paletteTableCache[indexName];
    }
    const stops = SATELLITE_PALETTE_STOPS[indexName];
    const steps = 256;
    const segments = stops.length - 1;
    const table = new Uint8ClampedArray(steps * 3);
    for (let i = 0; i < steps; i++) {
        const t = (i / (steps - 1)) * segments;
        const segment = Math.min(Math.floor(t), segments - 1);
        const frac = t - segment;
        const a = stops[segment];
        const b = stops[segment + 1];
        table[(i * 3) + 0] = Math.round(a[0] + ((b[0] - a[0]) * frac));
        table[(i * 3) + 1] = Math.round(a[1] + ((b[1] - a[1]) * frac));
        table[(i * 3) + 2] = Math.round(a[2] + ((b[2] - a[2]) * frac));
    }
    paletteTableCache[indexName] = table;
    return table;
}

// Reads FarmOpenfieldApiController.EnsureGridRawAsync's wire format (4-byte width + 4-byte height, little-endian, then one byte per pixel; 0xFF = invalid/masked) and paints it into an off-screen canvas, same per-pixel palette logic as SatellitePaletteRenderer.RenderPng but run in the browser instead of on the server.
async function renderGridToDataUrl(url, indexName) {
    const response = await fetch(url);
    if (!response.ok) {
        return null;
    }
    const buffer = await response.arrayBuffer();
    const view = new DataView(buffer);
    const width = view.getInt32(0, true);
    const height = view.getInt32(4, true);
    const pixels = new Uint8Array(buffer, 8, width * height);
    const table = paletteTableFor(indexName);

    const canvas = document.createElement('canvas');
    canvas.width = width;
    canvas.height = height;
    const ctx = canvas.getContext('2d');
    const imageData = ctx.createImageData(width, height);
    for (let i = 0; i < pixels.length; i++) {
        const value = pixels[i];
        const o = i * 4;
        if (value === 0xFF) {
            continue; // ImageData starts zero-filled - alpha stays 0, same "transparent invalid pixel" as the server renderer
        }
        imageData.data[o] = table[(value * 3) + 0];
        imageData.data[o + 1] = table[(value * 3) + 1];
        imageData.data[o + 2] = table[(value * 3) + 2];
        imageData.data[o + 3] = 255;
    }
    ctx.putImageData(imageData, 0, 0);
    return canvas.toDataURL();
}

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
    let renderGeneration = 0; // guards against a slow grid fetch resolving after a newer loadAndRender already cleared/repopulated overlayLayer

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
        const myGeneration = ++renderGeneration;
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
                const indexName = SATELLITE_INDEX_NAMES[parseInt(indexValue, 10)];
                if (SATELLITE_PALETTE_STOPS[indexName]) {
                    // Scalar index - fetch the raw grid and palette-render it in the browser instead of asking the server for a pre-rendered PNG.
                    const gridUrl = `/FarmOpenfield/SatelliteGrid?idFarmParcelZone=${z.zoneId}&idScene=${z.sceneId}&index=${indexValue}`;
                    renderGridToDataUrl(gridUrl, indexName).then((dataUrl) => {
                        if (dataUrl && myGeneration === renderGeneration) {
                            L.imageOverlay(dataUrl, zoneBounds, { opacity }).addTo(overlayLayer);
                        }
                    });
                } else {
                    // Composite (SwirComposite/NaturalColor) - already an RGB PNG, no scalar grid to palette-render.
                    // Same-origin passthrough (FarmOpenfieldController.SatelliteImage), not a direct Agrumy.Api link - a plain <img> can't carry the JWT Agrumy.Api requires.
                    const url = `/FarmOpenfield/SatelliteImage?idFarmParcelZone=${z.zoneId}&idScene=${z.sceneId}&index=${indexValue}`;
                    L.imageOverlay(url, zoneBounds, { opacity }).addTo(overlayLayer);
                }
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
