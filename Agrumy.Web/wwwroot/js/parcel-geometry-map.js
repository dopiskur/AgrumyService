// One Leaflet map showing a parcel's outer boundary plus each of its zones' subdivision
// polygons (S-A). Geoman edit mode is enabled on exactly one layer at a time - the "Editing"
// dropdown swaps which layer accepts draw/edit, everything else stays a read-only reference.
document.addEventListener('DOMContentLoaded', function () {
    const mapEl = document.getElementById('parcelGeometryMap');
    if (!mapEl || typeof L === 'undefined') {
        return;
    }

    const data = JSON.parse(mapEl.getAttribute('data-map'));
    const DEFAULT_LAT = 45.815;
    const DEFAULT_LON = 15.982;

    const map = L.map(mapEl).setView([DEFAULT_LAT, DEFAULT_LON], 15);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 20,
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
    }).addTo(map);

    const zoneColors = ['#2b8a3e', '#1971c2', '#e8590c', '#9c36b5', '#0c8599', '#c2255c'];

    // { key: 'parcel' | 'zone-<id>', layer: L.GeoJSON | null, group: L.FeatureGroup }
    const entries = [];

    function addEntry(key, geometryGeoJson, color) {
        const group = L.featureGroup().addTo(map);
        let layer = null;
        if (geometryGeoJson) {
            try {
                const parsed = JSON.parse(geometryGeoJson);
                layer = L.geoJSON(parsed, { style: { color, weight: 2, fillOpacity: 0.08 } });
                layer.eachLayer((l) => group.addLayer(l));
            } catch (e) {
                console.error('Invalid stored geometry for ' + key, e);
            }
        }
        entries.push({ key, group, color });
    }

    addEntry('parcel', data.parcel.geometry, '#495057');
    data.zones.forEach((z, i) => addEntry('zone-' + z.id, z.geometry, zoneColors[i % zoneColors.length]));

    const allBounds = L.latLngBounds([]);
    entries.forEach((e) => {
        e.group.eachLayer((l) => {
            if (l.getBounds) {
                allBounds.extend(l.getBounds());
            }
        });
    });
    if (allBounds.isValid()) {
        map.fitBounds(allBounds, { padding: [20, 20] });
    }

    const select = document.getElementById('geometryTargetSelect');
    const saveBtn = document.getElementById('geometrySaveBtn');
    if (!select || !saveBtn) {
        // Reader role - map is view-only, nothing else to wire up.
        return;
    }

    function currentEntry() {
        return entries.find((e) => e.key === select.value);
    }

    function setActive(key) {
        entries.forEach((e) => {
            const isActive = e.key === key;
            e.group.eachLayer((l) => {
                if (!l.pm) {
                    return;
                }
                if (isActive) {
                    l.pm.enable({ allowSelfIntersection: false });
                } else {
                    l.pm.disable();
                }
            });
            // Geoman's own draw toolbar always targets map.pm - only relevant while the active
            // group has no layer yet (first-time draw), so gate it on isActive + empty group.
        });
        const active = entries.find((e) => e.key === key);
        const hasLayer = active && active.group.getLayers().length > 0;
        if (!hasLayer && active) {
            map.pm.enableDraw('Polygon', { pathOptions: { color: active.color, weight: 2 } });
        } else {
            map.pm.disableDraw();
        }
    }

    map.pm.addControls({ position: 'topleft', drawMarker: false, drawCircleMarker: false, drawPolyline: false, drawRectangle: false, drawCircle: false, drawText: false, editControls: false, dragMode: false, cutPolygon: false, removalMode: false });

    map.on('pm:create', (e) => {
        const active = currentEntry();
        if (!active) {
            return;
        }
        active.group.clearLayers();
        active.group.addLayer(e.layer);
        map.pm.disableDraw();
        e.layer.pm.enable({ allowSelfIntersection: false });
    });

    select.addEventListener('change', () => setActive(select.value));
    setActive(select.value);

    saveBtn.addEventListener('click', () => {
        const active = currentEntry();
        if (!active || active.group.getLayers().length === 0) {
            alert('Draw a boundary first.');
            return;
        }
        const layer = active.group.getLayers()[0];
        const geoJson = JSON.stringify(layer.toGeoJSON().geometry);
        if (active.key === 'parcel') {
            document.getElementById('parcelGeometryFormValue').value = geoJson;
            document.getElementById('parcelGeometryFormArkod').value = document.getElementById('arkodParcelIdInput').value;
            document.getElementById('parcelGeometryForm').submit();
        } else {
            document.getElementById('zoneGeometryFormZoneId').value = active.key.replace('zone-', '');
            document.getElementById('zoneGeometryFormValue').value = geoJson;
            document.getElementById('zoneGeometryForm').submit();
        }
    });
});
