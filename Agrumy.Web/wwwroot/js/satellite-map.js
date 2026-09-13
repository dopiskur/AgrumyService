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

// Days-of-week header and the grid renderer are shared module state, not per-widget - a calendar
// is stateless between renders, it just needs (container, data, what to highlight) each time.
const CALENDAR_DOW = ['Su', 'Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa'];
// A day marked red here always means "at least one zone's scene fell below 70% valid pixels that day".
const CALENDAR_UNRELIABLE_THRESHOLD = 70;
// Resolution-warning banner thresholds (updateSceneClassification) - see below for the two-tier wording.
const LOW_RESOLUTION_THRESHOLD = 30;
const DEGRADED_RESOLUTION_THRESHOLD = 60;

/// Draws one month of a calendar into `container` - days present in `availableDates` are clickable
/// and highlighted (green outline, red outline if `dateReliability` marks that day below CALENDAR_UNRELIABLE_THRESHOLD,
/// or solid blue if `selectedDateStr`), other days are disabled. `displayYearMonth` is 'YYYY-MM'.
/// Calls `onSelectDate(dateStr)` on a day click, `onNavigateMonth(newYearMonth)` on the prev/next month arrows -
/// the caller re-renders with the new state either way.
function renderCalendarGrid(container, availableDates, displayYearMonth, selectedDateStr, dateReliability, onSelectDate, onNavigateMonth) {
    const dateSet = new Set(availableDates);
    const [year, month] = displayYearMonth.split('-').map(Number); // month is 1-based
    const first = new Date(year, month - 1, 1);
    const daysInMonth = new Date(year, month, 0).getDate();
    const startDow = first.getDay();
    const monthLabel = first.toLocaleDateString('en-US', { month: 'long', year: 'numeric' });

    let html = '<div class="d-flex justify-content-between align-items-center mb-1">'
        + '<button type="button" class="btn btn-sm btn-outline-secondary py-0 px-2" data-cal-nav="-1">&lsaquo;</button>'
        + `<span class="small fw-semibold">${monthLabel}</span>`
        + '<button type="button" class="btn btn-sm btn-outline-secondary py-0 px-2" data-cal-nav="1">&rsaquo;</button>'
        + '</div><table class="table table-sm text-center mb-0" style="table-layout:fixed"><thead><tr>';
    CALENDAR_DOW.forEach((d) => { html += `<th class="small text-secondary fw-normal p-1">${d}</th>`; });
    html += '</tr></thead><tbody><tr>';
    for (let i = 0; i < startDow; i++) {
        html += '<td></td>';
    }
    let col = startDow;
    for (let day = 1; day <= daysInMonth; day++) {
        const dateStr = `${year}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
        const hasData = dateSet.has(dateStr);
        const isSelected = dateStr === selectedDateStr;
        const isUnreliable = hasData && dateReliability && dateReliability[dateStr] !== undefined && dateReliability[dateStr] < CALENDAR_UNRELIABLE_THRESHOLD;
        let cls = 'btn btn-sm w-100 p-1 border-0';
        cls += isSelected ? ' btn-primary' : isUnreliable ? ' btn-outline-danger text-danger' : hasData ? ' btn-outline-success' : ' text-secondary bg-transparent';
        html += `<td class="p-0"><button type="button" class="${cls}" ${hasData ? '' : 'disabled tabindex="-1"'} data-cal-date="${dateStr}">${day}</button></td>`;
        col++;
        if (col === 7 && day !== daysInMonth) {
            html += '</tr><tr>';
            col = 0;
        }
    }
    html += '</tr></tbody></table>';
    container.innerHTML = html;

    container.querySelectorAll('[data-cal-date]').forEach((btn) => {
        btn.addEventListener('click', () => onSelectDate(btn.getAttribute('data-cal-date')));
    });
    container.querySelectorAll('[data-cal-nav]').forEach((btn) => {
        btn.addEventListener('click', () => {
            const delta = parseInt(btn.getAttribute('data-cal-nav'), 10);
            const next = new Date(year, (month - 1) + delta, 1);
            onNavigateMonth(`${next.getFullYear()}-${String(next.getMonth() + 1).padStart(2, '0')}`);
        });
    });
}

function initSatelliteMap(mapId) {
    const mapEl = document.getElementById(mapId);
    if (!mapEl || typeof L === 'undefined') {
        return;
    }
    // "widget" wraps everything for one satellite instance - a single .card in the default layout
    // (_SatelliteMap.cshtml), or a two-column row when the map and its controls are drawn separately
    // (FarmParcelDetails' "Define boundaries" split layout) - either way this is the one scoping root.
    const widget = mapEl.closest('[data-sat-role="widget"]');
    const root = widget.querySelector('[data-sat-role="root"]');
    const scope = root.getAttribute('data-scope');
    const id = root.getAttribute('data-id');
    const canManage = root.getAttribute('data-can-manage') === '1';
    const indexRadios = widget.querySelectorAll('[data-sat-role="indexRadio"]');
    const resolutionWarning = widget.querySelector('[data-sat-role="resolutionWarning"]');
    const sceneClassificationEl = widget.querySelector('[data-sat-role="sceneClassification"]');
    const slider = widget.querySelector('[data-sat-role="dateSlider"]');
    const dateLabel = widget.querySelector('[data-sat-role="dateLabel"]');
    const legendEl = widget.querySelector('[data-sat-role="legend"]');
    const emptyNotice = widget.querySelector('[data-sat-role="emptyNotice"]');
    const syncButton = widget.querySelector('[data-sat-role="syncNow"]');
    // Calendar + Previous/Next are optional - only the split layout provides them, the plain slider still works everywhere else.
    const calendarEl = widget.querySelector('[data-sat-role="calendar"]');
    const prevDateBtn = widget.querySelector('[data-sat-role="prevDate"]');
    const nextDateBtn = widget.querySelector('[data-sat-role="nextDate"]');

    const map = L.map(mapEl);
    // Same-origin passthrough (Agrumy.Web/Controllers/View/MapController.cs) to Agrumy.Api's TileProxy - a plain <img> tile request can't carry the JWT that TileProxy's [Authorize] requires. Not added to the map by default - "ARKOD karta" is, see below.
    const osmLayer = L.tileLayer(`/Map/Tile/{z}/{x}/{y}.png`, {
        maxZoom: 19,
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
    });
    // "ARKOD karta" base option layers the ARKOD (hr.land_parcels) WMS boundary overlay - public NIPP-registered service, no registration needed - over real aerial imagery (ARKOD's own WMS has no imagery layer, only vector boundaries) so it reads as an actual satellite map. Default base layer.
    const arkodBaseLayer = L.layerGroup([
        L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}', {
            maxZoom: 19,
            attribution: 'Tiles &copy; Esri &mdash; Source: Esri, Maxar, Earthstar Geographics, and the GIS User Community',
        }),
        L.tileLayer.wms('https://servisi.apprrr.hr/NIPP/wms', { layers: 'hr.land_parcels', format: 'image/png', transparent: true, version: '1.3.0', attribution: 'ARKOD &copy; APPRRR/NIPP' }),
    ]).addTo(map);
    L.control.layers({ 'ARKOD karta': arkodBaseLayer, 'OpenStreetMap': osmLayer }).addTo(map);
    map.setView([45.815, 15.982], 13);

    let overlayLayer = L.layerGroup().addTo(map);
    let availableDates = [];
    let dateReliability = {}; // dateStr -> minValidPixelPercent, for the calendar's fixed-70% red marking
    let selectedIndex = -1; // index into availableDates - the one shared "current date" state, slider/calendar/prev-next all just move this
    let calendarDisplayMonth = null; // 'YYYY-MM' the calendar grid is currently showing, independent of which day is selected
    let renderGeneration = 0; // guards against a slow grid fetch resolving after a newer loadAndRender already cleared/repopulated overlayLayer

    function selectedDate() {
        return selectedIndex >= 0 && selectedIndex < availableDates.length ? availableDates[selectedIndex] : null;
    }

    function getSelectedIndexValue() {
        for (const r of indexRadios) {
            if (r.checked) {
                return r.value;
            }
        }
        return indexRadios.length ? indexRadios[0].value : '1';
    }

    function updateLegend() {
        const indexName = SATELLITE_INDEX_NAMES[parseInt(getSelectedIndexValue(), 10)];
        legendEl.textContent = SATELLITE_LEGENDS[indexName] || '';
    }

    /// Scene classification readout + the resolution-warning banner - both use the worst (lowest
    /// ValidPixelPercent) zone for the loaded date, same "flag the worst case" rule as the calendar.
    /// Below LOW_RESOLUTION_THRESHOLD the remaining valid pixels are few enough that they risk being an
    /// unrepresentative subset of the parcel rather than just a noisier sample - a harder warning than
    /// the DEGRADED_RESOLUTION_THRESHOLD band, which is still a fine sample for a trend chart.
    function updateSceneClassification(worstValidPixelPercent, worstCloudPercent) {
        if (worstValidPixelPercent === null) {
            sceneClassificationEl.textContent = '';
            resolutionWarning.hidden = true;
            return;
        }
        sceneClassificationEl.textContent = `Scene classification: ${worstCloudPercent.toFixed(0)}% cloud, ${worstValidPixelPercent.toFixed(0)}% valid pixels.`;
        const pct = worstValidPixelPercent.toFixed(0);
        if (worstValidPixelPercent < LOW_RESOLUTION_THRESHOLD) {
            resolutionWarning.className = 'alert alert-danger py-1 px-2 small mb-2';
            resolutionWarning.textContent = `Warning: Low resolution ${pct}%, statistical representation may be skewed.`;
            resolutionWarning.hidden = false;
        } else if (worstValidPixelPercent < DEGRADED_RESOLUTION_THRESHOLD) {
            resolutionWarning.className = 'alert alert-warning py-1 px-2 small mb-2';
            resolutionWarning.textContent = `Warning: Resolution degraded ${pct}%.`;
            resolutionWarning.hidden = false;
        } else {
            resolutionWarning.hidden = true;
        }
    }

    function updatePrevNextButtons() {
        if (prevDateBtn) {
            prevDateBtn.disabled = selectedIndex <= 0;
        }
        if (nextDateBtn) {
            nextDateBtn.disabled = selectedIndex < 0 || selectedIndex >= availableDates.length - 1;
        }
    }

    function refreshCalendar() {
        if (!calendarEl) {
            return;
        }
        const date = selectedDate();
        if (date) {
            calendarDisplayMonth = date.slice(0, 7);
        } else if (!calendarDisplayMonth) {
            const today = new Date();
            calendarDisplayMonth = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}`;
        }
        renderCalendarGrid(calendarEl, availableDates, calendarDisplayMonth, date, dateReliability, (clickedDate) => {
            const idx = availableDates.indexOf(clickedDate);
            if (idx >= 0) {
                selectedIndex = idx;
                loadAndRender();
            }
        }, (newMonth) => {
            calendarDisplayMonth = newMonth;
            refreshCalendar();
        });
    }

    async function loadDates() {
        const response = await fetch(`/FarmOpenfield/SatelliteMapDates?scope=${scope}&id=${id}`);
        const entries = response.ok ? await response.json() : [];
        availableDates = entries.map((e) => e.date);
        dateReliability = {};
        entries.forEach((e) => { dateReliability[e.date] = e.minValidPixelPercent; });
        if (availableDates.length === 0) {
            selectedIndex = -1;
            if (slider) {
                slider.disabled = true;
                slider.max = 0;
            }
            dateLabel.textContent = 'No dates yet';
            emptyNotice.hidden = false;
            updatePrevNextButtons();
            refreshCalendar();
            return false;
        }
        emptyNotice.hidden = true;
        selectedIndex = availableDates.length - 1; // default to the most recent date
        if (slider) {
            slider.disabled = false;
            slider.max = availableDates.length - 1;
            slider.value = selectedIndex;
        }
        dateLabel.textContent = availableDates[selectedIndex];
        updatePrevNextButtons();
        refreshCalendar();
        return true;
    }

    async function loadAndRender() {
        const myGeneration = ++renderGeneration;
        const date = selectedDate();
        dateLabel.textContent = date || 'No dates yet';
        if (slider) {
            slider.value = selectedIndex >= 0 ? selectedIndex : 0;
        }
        updatePrevNextButtons();
        refreshCalendar();
        const indexValue = getSelectedIndexValue();

        let url = `/FarmOpenfield/SatelliteMap?scope=${scope}&id=${id}&index=${indexValue}`;
        if (date) {
            url += `&date=${date}`;
        }
        const response = await fetch(url);
        if (!response.ok) {
            return;
        }
        const data = await response.json();

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

        let worstValidPixelPercent = null;
        let worstCloudPercent = null;
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

            if (!z.hasData || !z.sceneId) {
                return;
            }
            if (worstValidPixelPercent === null || z.validPixelPercent < worstValidPixelPercent) {
                worstValidPixelPercent = z.validPixelPercent;
                worstCloudPercent = z.cloudPercent;
            }

            const indexName = SATELLITE_INDEX_NAMES[parseInt(indexValue, 10)];
            if (SATELLITE_PALETTE_STOPS[indexName]) {
                // Scalar index - fetch the raw grid and palette-render it in the browser instead of asking the server for a pre-rendered PNG.
                const gridUrl = `/FarmOpenfield/SatelliteGrid?idFarmParcelZone=${z.zoneId}&idScene=${z.sceneId}&index=${indexValue}`;
                renderGridToDataUrl(gridUrl, indexName).then((dataUrl) => {
                    if (dataUrl && myGeneration === renderGeneration) {
                        L.imageOverlay(dataUrl, zoneBounds).addTo(overlayLayer);
                    }
                });
            } else {
                // Composite (SwirComposite/NaturalColor) - already an RGB PNG, no scalar grid to palette-render.
                // Same-origin passthrough (FarmOpenfieldController.SatelliteImage), not a direct Agrumy.Api link - a plain <img> can't carry the JWT Agrumy.Api requires.
                const rasterUrl = `/FarmOpenfield/SatelliteImage?idFarmParcelZone=${z.zoneId}&idScene=${z.sceneId}&index=${indexValue}`;
                L.imageOverlay(rasterUrl, zoneBounds).addTo(overlayLayer);
            }
        });

        if (bounds.isValid()) {
            map.fitBounds(bounds, { padding: [20, 20] });
        }
        updateSceneClassification(worstValidPixelPercent, worstCloudPercent);
    }

    indexRadios.forEach((r) => r.addEventListener('change', () => { updateLegend(); loadAndRender(); }));
    if (slider) {
        slider.addEventListener('input', () => { selectedIndex = parseInt(slider.value, 10); loadAndRender(); });
    }
    if (prevDateBtn) {
        prevDateBtn.addEventListener('click', () => { if (selectedIndex > 0) { selectedIndex--; loadAndRender(); } });
    }
    if (nextDateBtn) {
        nextDateBtn.addEventListener('click', () => { if (selectedIndex < availableDates.length - 1) { selectedIndex++; loadAndRender(); } });
    }

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
