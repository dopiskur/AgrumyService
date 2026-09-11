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

    // ARKOD (hr.land_parcels) WMS visual overlay - public NIPP-registered service, no registration needed.
    const ARKOD_WMS_URL = 'https://servisi.apprrr.hr/NIPP/wms';
    const ARKOD_WMS_LAYER = 'hr.land_parcels';
    const arkodWmsLayer = L.tileLayer.wms(ARKOD_WMS_URL, {
        layers: ARKOD_WMS_LAYER,
        format: 'image/png',
        transparent: true,
        version: '1.3.0',
        attribution: 'ARKOD &copy; APPRRR/NIPP',
    });
    const arkodWmsToggle = document.getElementById('arkodWmsToggle');
    if (arkodWmsToggle) {
        if (arkodWmsToggle.checked) {
            arkodWmsLayer.addTo(map);
        }
        arkodWmsToggle.addEventListener('change', () => {
            if (arkodWmsToggle.checked) {
                arkodWmsLayer.addTo(map);
            } else {
                map.removeLayer(arkodWmsLayer);
            }
        });
    }

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

    // Shared by both ARKOD lookup paths below: drops the returned boundary into the layer
    // "Editing" currently targets with the same Geoman edit handles a manual draw would get, fills
    // the ARKOD ID field (parcel target only), and reports what was found.
    function applyArkodGeometry(active, geometryGeoJson, arkodId, areaM2) {
        const geoLayer = L.geoJSON(geometryGeoJson, { style: { color: active.color, weight: 2, fillOpacity: 0.08 } });
        active.group.clearLayers();
        geoLayer.eachLayer((l) => {
            active.group.addLayer(l);
            if (l.pm) {
                l.pm.enable({ allowSelfIntersection: false });
            }
        });
        map.pm.disableDraw();
        map.fitBounds(active.group.getBounds(), { padding: [20, 20] });

        if (active.key === 'parcel') {
            const arkodInput = document.getElementById('arkodParcelIdInput');
            if (arkodInput) {
                arkodInput.value = arkodId;
            }
        }
        const areaHa = typeof areaM2 === 'number' ? (areaM2 / 10000).toFixed(2) : null;
        lookupStatus.textContent = `Found ARKOD parcel ${arkodId}${areaHa ? ' (' + areaHa + ' ha)' : ''} - adjust the boundary if needed, then Save boundary.`;
    }

    // "Find ARKOD parcel by click": one armed map click runs a WMS GetFeatureInfo query against
    // the currently-visible ARKOD overlay.
    const lookupBtn = document.getElementById('arkodLookupBtn');
    const lookupStatus = document.getElementById('arkodLookupStatus');
    let lookupArmed = false;

    function setLookupArmed(armed) {
        lookupArmed = armed;
        mapEl.style.cursor = armed ? 'crosshair' : '';
        if (lookupBtn) {
            lookupBtn.classList.toggle('active', armed);
        }
    }

    if (lookupBtn) {
        lookupBtn.addEventListener('click', () => {
            if (lookupArmed) {
                setLookupArmed(false);
                if (lookupStatus) {
                    lookupStatus.hidden = true;
                }
                return;
            }
            if (!currentEntry()) {
                return;
            }
            map.pm.disableDraw();
            setLookupArmed(true);
            lookupStatus.hidden = false;
            lookupStatus.textContent = 'Click a point on the map to look up the ARKOD parcel there.';
        });
    }

    map.on('click', async (e) => {
        if (!lookupArmed) {
            return;
        }
        setLookupArmed(false);
        const active = currentEntry();
        if (!active) {
            return;
        }
        lookupStatus.textContent = 'Looking up ARKOD parcel...';

        const bounds = map.getBounds();
        const size = map.getSize();
        const point = map.latLngToContainerPoint(e.latlng);
        const url = `${ARKOD_WMS_URL}?SERVICE=WMS&VERSION=1.3.0&REQUEST=GetFeatureInfo&LAYERS=${ARKOD_WMS_LAYER}&QUERY_LAYERS=${ARKOD_WMS_LAYER}` +
            `&CRS=CRS:84&BBOX=${bounds.getWest()},${bounds.getSouth()},${bounds.getEast()},${bounds.getNorth()}` +
            `&WIDTH=${size.x}&HEIGHT=${size.y}&I=${Math.round(point.x)}&J=${Math.round(point.y)}&INFO_FORMAT=application/json&FEATURE_COUNT=1`;

        try {
            const response = await fetch(url);
            const data = response.ok ? await response.json() : null;
            const feature = data && data.features && data.features[0];
            if (!feature) {
                lookupStatus.textContent = 'No ARKOD parcel found at that point - zoom in and try again, or draw manually.';
                return;
            }

            const arkodId = feature.properties?.jpaid || feature.properties?.id || '';
            applyArkodGeometry(active, feature.geometry, arkodId, feature.properties?.area);
        } catch (err) {
            lookupStatus.textContent = 'ARKOD lookup failed (network error) - try again or draw manually.';
        }
    });

    // "Look up this ID" - the offline-capable counterpart: resolves the typed ARKOD ID against this
    // server's own local GeoPackage mirror (FarmOpenfieldController.ArkodLookupByJpaId) instead of
    // querying servisi.apprrr.hr directly, so it still works with no outbound internet at request time.
    const lookupByIdBtn = document.getElementById('arkodLookupByIdBtn');
    if (lookupByIdBtn) {
        lookupByIdBtn.addEventListener('click', async () => {
            const active = currentEntry();
            const jpaid = document.getElementById('arkodParcelIdInput')?.value?.trim();
            if (!active || !jpaid) {
                return;
            }
            lookupStatus.hidden = false;
            lookupStatus.textContent = 'Looking up ARKOD parcel ' + jpaid + '...';
            try {
                const response = await fetch(`/FarmOpenfield/ArkodLookupByJpaId?jpaid=${encodeURIComponent(jpaid)}`);
                if (!response.ok) {
                    lookupStatus.textContent = response.status === 404
                        ? 'No ARKOD parcel with that ID in the local GeoPackage mirror.'
                        : 'ARKOD lookup failed - is GeoPackage sync/upload configured on Server Settings?';
                    return;
                }
                const result = await response.json();
                applyArkodGeometry(active, JSON.parse(result.geometryGeoJson), result.arkodParcelId, result.areaM2);
            } catch (err) {
                lookupStatus.textContent = 'ARKOD lookup failed (network error).';
            }
        });
    }
});
