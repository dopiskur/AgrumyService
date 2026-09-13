using Agrumy.Web.Security;
using Agrumy.Web.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Agrumy.Web.Utils;
using Agrumy.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agrumy.Web.Controllers.View
{
    /// Sowing/FarmParcel/FarmParcelZone CRUD, the sjetva wizard's Start/Close lifecycle, device assignment, and safety-limit editing (D1/D2/D9) - the Open-Field mirror of DeviceFarmUnitController's Zone/Unit pages. Farm-level actions (Add/Rename/Delete/rule pages) stay on DeviceFarmUnitController, shared by both branches - this controller only owns what's genuinely new. No standalone "Farms" register page any more - creating a new Open-Field farm and the per-farm Satellite link both live inline on CropSeasons.cshtml/FarmGroup/Details.cshtml now.

    [Authorize]
    public class FarmOpenfieldController(IApi api) : Controller
    {
        // ---- Crop Seasons (every sowing across every Open-Field farm) --------------------------------------------

        public async Task<ActionResult> CropSeasons()
        {
            List<DeviceFarm> farms = (await api.DeviceFarmsGet()).Where(f => f.FarmType == FarmType.OpenField).ToList();
            List<int?> farmIds = farms.Select(f => f.IDDeviceFarm).ToList();
            IList<Sowing> sowings = (await api.CropsGet()).Where(s => farmIds.Contains(s.FarmID)).ToList();

            // Parcel/group picker is keyed per farm so the wizard can swap between them client-side as the Farm choice changes.
            var availableParcelsByFarm = new Dictionary<int, IList<FarmParcelWithZonesViewModel>>();
            var parcelGroupsByFarm = new Dictionary<int, IList<FarmParcelGroupCrop>>();
            foreach (DeviceFarm farm in farms)
            {
                int idFarm = farm.IDDeviceFarm!.Value;
                var availableParcels = new List<FarmParcelWithZonesViewModel>();
                foreach (FarmParcel parcel in await api.FarmParcelsGet(idFarm))
                {
                    availableParcels.Add(new FarmParcelWithZonesViewModel { Parcel = parcel, Zones = await api.FarmParcelZonesGet(parcel.IDFarmParcel!.Value) });
                }
                availableParcelsByFarm[idFarm] = availableParcels;
                parcelGroupsByFarm[idFarm] = await api.ParcelGroupsGet(idFarm);
            }

            IList<FarmGroup> farmGroups = await api.FarmGroupsGet();

            return View(new CropSeasonsIndexViewModel
            {
                Farms = farms,
                Sowings = sowings,
                CatalogCrops = await api.CropCatalogGet(CropCatalogType.Arable),
                AvailableParcelsByFarm = availableParcelsByFarm,
                ParcelGroupsByFarm = parcelGroupsByFarm,
                FarmGroupNames = farmGroups.Where(g => g.IDFarmGroup is int).ToDictionary(g => g.IDFarmGroup!.Value, g => g.Name ?? ""),
            });
        }

        // ---- Parcel boundary + zone separations on a Leaflet+Geoman map (S-A) --------------------

        public async Task<ActionResult> FarmParcelDetails(int idFarmParcel)
        {
            FarmParcel parcel = await api.FarmParcelGeometryGet(idFarmParcel);
            IList<FarmParcelZone> zones = await api.FarmParcelZonesGet(idFarmParcel);
            return View(new FarmParcelWithZonesViewModel { Parcel = parcel, Zones = zones });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> FarmParcelGeometrySet(int idFarmParcel, string geometryGeoJson, string? arkodParcelId)
        {
            try
            {
                await api.FarmParcelGeometrySet(idFarmParcel, new ParcelGeometrySetRequest { GeometryGeoJson = geometryGeoJson, ArkodParcelId = arkodParcelId });
                TempData["Message"] = "Parcel boundary saved.";
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(FarmParcelDetails), new { idFarmParcel });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ParcelZoneGeometrySet(int idFarmParcelZone, int idFarmParcel, string geometryGeoJson)
        {
            try
            {
                await api.ParcelZoneGeometrySet(idFarmParcelZone, new ParcelGeometrySetRequest { GeometryGeoJson = geometryGeoJson });
                TempData["Message"] = "Zone boundary saved.";
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(FarmParcelDetails), new { idFarmParcel });
        }

        // ---- Satellite four-level map (S-C) ----------------------------

        /// Farm-tab entry point - a dedicated page rather than embedding a map per farm card on Index (N farms would mean N eagerly-initialized Leaflet maps).
        public async Task<ActionResult> FarmSatellite(int idFarm)
        {
            DeviceFarm farm = await api.DeviceFarmGet(idFarm);
            ViewBag.FarmName = farm.DeviceFarmName;
            return View(idFarm);
        }

        // ---- Satellite four-level map proxy - thin JSON/binary proxy, the browser can't reach Agrumy.Api directly (different auth: Bearer vs. this app's cookie) ----------------------------

        public async Task<ActionResult<SatelliteMapResponse>> SatelliteMap(string scope, int id, int index, DateOnly? date) =>
            Json(await api.SatelliteMapGet(scope, id, index, date));

        public async Task<ActionResult<IList<SatelliteDateEntry>>> SatelliteMapDates(string scope, int id) =>
            Json(await api.SatelliteMapDatesGet(scope, id));

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SatelliteSyncNow(string scope, int id)
        {
            try
            {
                await api.SatelliteMapSyncNow(scope, id);
                return Ok();
            }
            catch (ApiException ex)
            {
                return StatusCode(ex.StatusCode == 0 ? 500 : ex.StatusCode, ex.Body);
            }
        }

        public async Task<ActionResult> SatelliteImage(int idFarmParcelZone, int idScene, int index)
        {
            HttpResponseMessage response = await api.SatelliteIndexPngGet(idFarmParcelZone, idScene, index);
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode);
            }
            return File(await response.Content.ReadAsStreamAsync(), "image/png");
        }

        /// Raw scalar-index grid (width/height header + one byte per pixel) for satellite-map.js to palette-render client-side instead of a server-rendered PNG - same-origin passthrough, same reason as SatelliteImage above.
        public async Task<ActionResult> SatelliteGrid(int idFarmParcelZone, int idScene, int index)
        {
            HttpResponseMessage response = await api.SatelliteIndexGridGet(idFarmParcelZone, idScene, index);
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode);
            }
            return File(await response.Content.ReadAsStreamAsync(), "application/octet-stream");
        }

        public async Task<ActionResult<IList<SatelliteSeriesPoint>>> SatelliteSeries(int idFarmParcelZone, int index, DateOnly? from, DateOnly? to, bool onlyReliable = true) =>
            Json(await api.SatelliteSeriesGet(idFarmParcelZone, index, from, to, onlyReliable));

        public async Task<ActionResult<IList<FarmParcelZoneMoistureSeriesPoint>>> MoistureSeries(int idFarmParcelZone, DateOnly from, DateOnly to) =>
            Json(await api.MoistureSeriesGet(idFarmParcelZone, from, to));

        /// The offline-capable counterpart to the browser-direct WMS click-lookup in parcel-geometry-map.js: looks a known ARKOD ID up against the local GeoPackage mirror instead of servisi.apprrr.hr.
        public async Task<ActionResult> ArkodLookupById(string arkodId)
        {
            try
            {
                return Json(await api.ArkodLookup(arkodId));
            }
            catch (ApiException ex)
            {
                return StatusCode(ex.StatusCode == 0 ? 500 : ex.StatusCode, ex.Body);
            }
        }

        // ---- Sowing CRUD --------------------------------------------------

        /// Sjetva wizard (D3/D9): crop (picked from the Crop Catalog's Arable entries - wheat/corn + variety, BBCH-staged; resolved/created in the separate lightweight Crop catalog server-side by that same name) + start date + an OPTIONAL expected end date (not a hard deadline, just the estimate ExpectedDurationDays is derived from - falls back to DefaultExpectedDurationDays when left blank, since neither Sowing nor CropCatalogEntry carries a per-crop growth-length default). No field-operation picker here - ploughing/fertilizing/etc. are dnevnik entries added once the sowing exists (FieldLogEntryAdd on the Details page), not part of this form. When the wizard's parcel/group picker supplied individual zones and/or parcel groups, the sowing is created AND started in this one request (group ids resolve to their member parcels' zones, deduplicated against any individually-picked ones) instead of being left Planned for a manual Start step; an empty selection keeps the old create-as-Planned behavior.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CropAdd(int idFarm, string farmOpenfieldCropName, string? sowingName, string? variety, DateOnly startDate, DateOnly? expectedEndDate, List<int>? farmParcelZoneIds, List<int>? farmParcelGroupCropIds)
        {
            const int defaultExpectedDurationDays = 90;
            int expectedDurationDays = expectedEndDate is DateOnly eed ? Math.Max(1, eed.DayNumber - startDate.DayNumber) : defaultExpectedDurationDays;
            Sowing added = await api.CropAdd(new Sowing
            {
                FarmID = idFarm,
                SowingName = farmOpenfieldCropName,
                Name = string.IsNullOrWhiteSpace(sowingName) ? null : sowingName,
                Variety = variety,
                StartDate = startDate,
                ExpectedDurationDays = expectedDurationDays,
            });

            var zoneIds = new HashSet<int>(farmParcelZoneIds ?? []);
            foreach (int idGroup in farmParcelGroupCropIds ?? [])
            {
                foreach (int idZone in await api.ParcelGroupZonesGet(idGroup))
                {
                    zoneIds.Add(idZone);
                }
            }
            if (zoneIds.Count > 0)
            {
                try
                {
                    await api.SowingStart(new SowingStartRequest { IDSowing = added.IDSowing!.Value, FarmParcelZoneIds = zoneIds.ToList() });
                    TempData["Message"] = "Sowing created and started.";
                }
                catch (ApiException ex)
                {
                    TempData["Error"] = ex.Body;
                }
            }
            return RedirectToAction(nameof(Parcels), new { idSowing = added.IDSowing });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CropRename(int idSowing, string sowingName)
        {
            Sowing crop = await api.CropGet(idSowing);
            crop.Name = sowingName;
            await api.CropUpdate(crop);
            return RedirectToAction(nameof(Parcels), new { idSowing });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CropDelete(int idSowing)
        {
            await api.CropDelete(idSowing);
            return RedirectToAction(nameof(CropSeasons));
        }

        /// D9 - Planned -> Active: occupies the picked zones, redirects back to Sowing Details.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SowingStart(int idSowing, List<int> farmParcelZoneIds)
        {
            try
            {
                await api.SowingStart(new SowingStartRequest { IDSowing = idSowing, FarmParcelZoneIds = farmParcelZoneIds });
                TempData["Message"] = "Sowing started.";
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Parcels), new { idSowing });
        }

        /// Manage-parcels dialog's "Add parcel group" - expands the group to its member zones and starts/extends the sowing with whichever of them aren't already occupied by it (SowingStart itself rejects any that are free-elsewhere/occupied-elsewhere).
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SowingAddGroup(int idSowing, int idFarmParcelGroupCrop)
        {
            var currentZoneIds = (await api.ParcelsGet(idSowing)).Select(z => z.IDFarmParcelZone).ToHashSet();
            var zoneIds = (await api.ParcelGroupZonesGet(idFarmParcelGroupCrop)).Where(id => !currentZoneIds.Contains(id)).ToList();
            if (zoneIds.Count == 0)
            {
                TempData["Message"] = "Every parcel in that group is already part of this sowing.";
                return RedirectToAction(nameof(Parcels), new { idSowing });
            }
            try
            {
                await api.SowingStart(new SowingStartRequest { IDSowing = idSowing, FarmParcelZoneIds = zoneIds });
                TempData["Message"] = "Parcel group added.";
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Parcels), new { idSowing });
        }

        /// Manage-parcels dialog's Remove on a single zone - releases it without closing the sowing or touching any other zone it still occupies.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SowingReleaseZone(int idSowing, int idFarmParcelZone)
        {
            try
            {
                await api.SowingReleaseZone(new SowingReleaseZoneRequest { IDSowing = idSowing, FarmParcelZoneID = idFarmParcelZone });
                TempData["Message"] = "Parcel removed from sowing.";
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Parcels), new { idSowing });
        }

        /// Manage-parcels dialog's "Remove parcel group <name>" option (as opposed to "Remove parcel from the parcel group", which is ParcelGroupRemoveMember) - releases every zone this sowing currently occupies that belongs to the group, leaving the group definition itself untouched.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SowingReleaseGroup(int idSowing, int idFarmParcelGroupCrop)
        {
            var currentZoneIds = (await api.ParcelsGet(idSowing)).Select(z => z.IDFarmParcelZone).ToHashSet();
            var groupZoneIds = await api.ParcelGroupZonesGet(idFarmParcelGroupCrop);
            foreach (int idZone in groupZoneIds.Where(id => currentZoneIds.Contains(id)))
            {
                await api.SowingReleaseZone(new SowingReleaseZoneRequest { IDSowing = idSowing, FarmParcelZoneID = idZone });
            }
            TempData["Message"] = "Parcel group removed from sowing.";
            return RedirectToAction(nameof(Parcels), new { idSowing });
        }

        /// D9/D13/D14 - Active -> Closed: a grouped harvest result (per-zone breakdown is a fast-follow), releases every occupied zone. confirmEarlyHarvest is D13's explicit "I know the karenca hasn't passed yet" checkbox, shown on the page whenever EarliestHarvestDate is still in the future.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SowingClose(int idSowing, double yieldKg, double? moisturePercent, string? qualityGrade, string? note, bool confirmEarlyHarvest)
        {
            try
            {
                await api.SowingClose(new SowingCloseRequest { IDSowing = idSowing, YieldKg = yieldKg, MoisturePercent = moisturePercent, QualityGrade = qualityGrade, Note = note, Confirm = confirmEarlyHarvest });
                TempData["Message"] = "Sowing closed.";
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.StatusCode == 409
                    ? "Harvest is before the pre-harvest interval (PHI) has passed - check the confirmation box to proceed anyway."
                    : ex.Body;
            }
            return RedirectToAction(nameof(Parcels), new { idSowing });
        }

        // ---- Dnevnik (D6/D7) --------------------------------------------

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> FieldLogEntryAdd(int idSowing, FieldLogEntryFormInput input)
        {
            var entry = new FieldLogEntry
            {
                SowingID = idSowing,
                EntryType = input.EntryType,
                DateUtc = new DateTimeOffset(input.Date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
                Note = input.Note,
                PayloadJson = BuildPayloadJson(input),
            };
            try
            {
                await api.FieldLogEntryAdd(entry);
                TempData["Message"] = $"{input.EntryType} logged.";
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Parcels), new { idSowing });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> FieldLogEntryDelete(int idFieldLogEntry, int idSowing)
        {
            await api.FieldLogEntryDelete(idFieldLogEntry);
            return RedirectToAction(nameof(Parcels), new { idSowing });
        }

        public async Task<ActionResult> PlantProtectionReport(int idSowing)
        {
            HttpResponseMessage response = await api.PlantProtectionReportGet(idSowing);
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode);
            }
            string downloadName = response.Content.Headers.ContentDisposition?.FileNameStar
                ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                ?? "plant-protection-report.csv";
            return File(await response.Content.ReadAsStreamAsync(), "text/csv", downloadName);
        }

        /// Only the family of fields the given EntryType actually stores gets serialized (D7) - everything else (Ploughing, Discing, Weeding, Observation, Other, ...) has no structured payload, just the shared Note field. Internal: also reused by DeviceFarmUnitController for the greenhouse zonePlanting dnevnik, same payload catalog.
        internal static string? BuildPayloadJson(FieldLogEntryFormInput input) => input.EntryType switch
        {
            EntryType.Fertilization or EntryType.BaseFertilization => System.Text.Json.JsonSerializer.Serialize(new FertilizationPayload
            {
                Product = input.Product,
                NPercent = input.NPercent,
                PPercent = input.PPercent,
                KPercent = input.KPercent,
                DoseKgPerHa = input.DoseKgPerHa ?? 0,
                AreaHa = input.AreaHa ?? 0,
            }),
            EntryType.SoilAnalysis => System.Text.Json.JsonSerializer.Serialize(new SoilAnalysisPayload
            {
                PH = input.PH,
                HumusPercent = input.HumusPercent,
                P2O5 = input.P2O5,
                K2O = input.K2O,
                NMin = input.NMin,
                DepthCm = input.DepthCm,
                Laboratory = input.Laboratory,
            }),
            EntryType.PlantProtection => System.Text.Json.JsonSerializer.Serialize(new PlantProtectionPayload
            {
                ProductName = input.ProductName ?? "",
                ActiveSubstance = input.ActiveSubstance ?? "",
                Dose = input.Dose ?? "",
                TreatedAreaHa = input.TreatedAreaHa ?? 0,
                Reason = input.Reason ?? "",
                PhiDays = input.PhiDays ?? 0,
                Applicator = input.Applicator ?? "",
                WeatherConditions = input.WeatherConditions,
            }),
            EntryType.Irrigation => System.Text.Json.JsonSerializer.Serialize(new IrrigationPayload
            {
                AmountMm = input.AmountMm,
                AmountM3 = input.AmountM3,
                DurationMinutes = input.DurationMinutes,
                Source = input.Source,
            }),
            _ => null,
        };

        // ---- Sowing Details (parcel/zone list + lifecycle) -----------------------------------

        public async Task<ActionResult> Parcels(int idSowing)
        {
            Sowing crop = await api.CropGet(idSowing);
            IList<DeviceFarm> farms = await api.DeviceFarmsGet();
            DeviceFarm? farm = farms.FirstOrDefault(f => f.IDDeviceFarm == crop.FarmID);

            var availableParcels = new List<FarmParcelWithZonesViewModel>();
            foreach (FarmParcel parcel in await api.FarmParcelsGet(crop.FarmID))
            {
                availableParcels.Add(new FarmParcelWithZonesViewModel { Parcel = parcel, Zones = await api.FarmParcelZonesGet(parcel.IDFarmParcel!.Value) });
            }

            return View(new CropParcelsViewModel
            {
                Crop = crop,
                Farm = farm ?? new DeviceFarm(),
                Parcels = await api.ParcelDashboardListGet(idSowing),
                AvailableParcels = availableParcels,
                ParcelGroups = await api.ParcelGroupsGet(crop.FarmID),
                LogEntries = await api.FieldLogEntriesGet(idSowing),
                EarliestHarvestDate = await api.EarliestHarvestDateGet(idSowing),
                NitrogenBalanceKgPerHa = await api.NitrogenBalanceGet(idSowing),
            });
        }

        // ---- Parcels registry (Fleet-style, every parcel/zone across every Open-Field farm) -----------------------------------

        /// Unified across all three parcel types (Crop/Fruit/Greenhouse) - Fruit has no rows yet (no module), Greenhouse stands in via DeviceFarmUnit (its own AreaHectares, no boundary map/Ready-for-season since those are Open-Field-only concepts).
        public async Task<ActionResult> ParcelsRegistry()
        {
            IList<DeviceFarm> allFarms = await api.DeviceFarmsGet();
            IList<DeviceFarm> openfieldFarms = allFarms.Where(f => f.FarmType == FarmType.OpenField).ToList();
            IList<FarmGroup> farmGroups = await api.FarmGroupsGet();
            var farmGroupNames = farmGroups.ToDictionary(g => g.IDFarmGroup!.Value, g => g.Name ?? "");
            var rows = new List<ParcelRegistryRowViewModel>();
            var farmOptions = new List<ParcelRegistryFarmOptionViewModel>();
            var groupSections = new List<ParcelGroupSectionViewModel>();
            var cropParcelsWithArea = 0;
            var cropParcelsTotal = 0;
            var cropAreaHa = 0.0;
            foreach (DeviceFarm farm in openfieldFarms)
            {
                int idFarm = farm.IDDeviceFarm!.Value;
                string? farmGroupName = farm.FarmGroupID is int idGroup ? farmGroupNames.GetValueOrDefault(idGroup) : null;
                farmOptions.Add(new ParcelRegistryFarmOptionViewModel { FarmName = farm.DeviceFarmName ?? "", IdFarm = idFarm });
                IList<FarmParcel> parcels = await api.FarmParcelsGet(idFarm);
                foreach (FarmParcel parcel in parcels)
                {
                    cropParcelsTotal++;
                    if (parcel.AreaHectares is double ha)
                    {
                        cropParcelsWithArea++;
                        cropAreaHa += ha;
                    }
                    foreach (FarmParcelZone zone in await api.FarmParcelZonesGet(parcel.IDFarmParcel!.Value))
                    {
                        rows.Add(new ParcelRegistryRowViewModel { IdFarm = idFarm, IdFarmGroup = farm.FarmGroupID, FarmGroupName = farmGroupName, Parcel = parcel, Zone = zone });
                    }
                }
                groupSections.Add(new ParcelGroupSectionViewModel
                {
                    FarmName = farm.DeviceFarmName ?? "",
                    IdFarm = idFarm,
                    Parcels = parcels,
                    Groups = await api.ParcelGroupsGet(idFarm),
                });
            }

            IList<DeviceFarmUnit> units = await api.DeviceFarmUnitsGet();
            var greenhouseFarms = allFarms.Where(f => f.FarmType == FarmType.Greenhouse).ToDictionary(f => f.IDDeviceFarm!.Value, f => f);
            var greenhouseRows = units
                .Select(u =>
                {
                    DeviceFarm? unitFarm = u.DeviceFarmID is int idUnitFarm ? greenhouseFarms.GetValueOrDefault(idUnitFarm) : null;
                    return new GreenhouseUnitRowViewModel
                    {
                        IdFarm = unitFarm?.IDDeviceFarm,
                        IdFarmGroup = unitFarm?.FarmGroupID,
                        FarmGroupName = unitFarm?.FarmGroupID is int idUnitGroup ? farmGroupNames.GetValueOrDefault(idUnitGroup) : null,
                        Unit = u,
                    };
                })
                .ToList();
            int greenhouseUnitsWithArea = units.Count(u => u.AreaHectares != null);
            double greenhouseAreaHa = units.Where(u => u.AreaHectares != null).Sum(u => u.AreaHectares!.Value);

            return View(new ParcelsRegistryViewModel
            {
                Rows = rows,
                Farms = farmOptions,
                GroupSections = groupSections,
                GreenhouseRows = greenhouseRows,
                CropAreaSummary = new ParcelAreaSummaryViewModel { TotalHectares = cropAreaHa, WithAreaCount = cropParcelsWithArea, TotalCount = cropParcelsTotal },
                GreenhouseAreaSummary = new ParcelAreaSummaryViewModel { TotalHectares = greenhouseAreaHa, WithAreaCount = greenhouseUnitsWithArea, TotalCount = units.Count },
                AvailableFarmGroups = farmGroups,
            });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> AssignFarmToGroup(int idFarm, int idFarmGroup)
        {
            await api.FarmAssignToGroup(idFarm, idFarmGroup);
            return RedirectToAction(nameof(ParcelsRegistry));
        }

        // ---- Parcel Groups (FarmParcelGroupCrop) management, from the ParcelsRegistry page -----------------------------------

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ParcelGroupAdd(int idFarm, string name, List<int>? memberParcelIds)
        {
            await api.ParcelGroupAdd(new FarmParcelGroupCrop { FarmID = idFarm, Name = name, MemberParcelIds = memberParcelIds ?? [] });
            return RedirectToAction(nameof(ParcelsRegistry));
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ParcelGroupDelete(int idFarmParcelGroupCrop)
        {
            await api.ParcelGroupDelete(idFarmParcelGroupCrop);
            return RedirectToAction(nameof(ParcelsRegistry));
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ParcelGroupAddMember(int idFarmParcelGroupCrop, int idFarmParcel, string? returnUrl)
        {
            await api.ParcelGroupAddMember(idFarmParcelGroupCrop, idFarmParcel);
            return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction(nameof(ParcelsRegistry));
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ParcelGroupRemoveMember(int idFarmParcelGroupCrop, int idFarmParcel, string? returnUrl)
        {
            await api.ParcelGroupRemoveMember(idFarmParcelGroupCrop, idFarmParcel);
            return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction(nameof(ParcelsRegistry));
        }

        /// The registry's own toggle - "ready" flips straight through, the "populate prep dates first" dialog lives client-side (parcel-registry.js) and just decides whether to detour through the Parcel detail page before/instead of calling this.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ParcelReadyForSeasonSet(int idFarmParcelZone, bool ready)
        {
            try
            {
                await api.ParcelReadyForSeasonSet(idFarmParcelZone, ready);
                return Ok();
            }
            catch (ApiException ex)
            {
                return StatusCode(ex.StatusCode == 0 ? 500 : ex.StatusCode, ex.Body);
            }
        }

        // ---- FarmParcel / FarmParcelZone CRUD (D2/D3/D4) -----------------------------------

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> FarmParcelAdd(int idFarm, string farmParcelName)
        {
            await api.FarmParcelAdd(idFarm, farmParcelName);
            return RedirectToAction(nameof(ParcelsRegistry));
        }

        /// D3/D4 - blocked server-side (409) while the zone has an active sowing; the form only offers this on free zones, so a conflict here means someone else started a sowing on it in the meantime.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ParcelSplit(int idFarmParcelZone, string newZoneNames)
        {
            List<string> names = newZoneNames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            try
            {
                await api.ParcelSplit(idFarmParcelZone, names);
                TempData["Message"] = "Zone split.";
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(ParcelsRegistry));
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ParcelMerge(List<int> farmParcelZoneIds, string mergedName)
        {
            try
            {
                await api.ParcelMerge(new ParcelMergeRequest { FarmParcelZoneIds = farmParcelZoneIds, MergedName = mergedName });
                TempData["Message"] = "Zones merged.";
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(ParcelsRegistry));
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ParcelDelete(int idFarmParcelZone, int idSowing)
        {
            await api.ParcelDelete(idFarmParcelZone);
            return RedirectToAction(nameof(Parcels), new { idSowing });
        }

        // ---- Parcel detail ------------------------------------------------

        public async Task<ActionResult> Parcel(int idFarmParcelZone)
        {
            ParcelViewModel model = await BuildParcelViewAsync(idFarmParcelZone);
            return View(model);
        }

        private async Task<ParcelViewModel> BuildParcelViewAsync(int idFarmParcelZone)
        {
            FarmParcelZone parcel = await api.ParcelGetById(idFarmParcelZone);
            Sowing? crop = parcel.CurrentSowingID is int idSowing ? await api.CropGet(idSowing) : null;
            IList<DeviceFarm> farms = await api.DeviceFarmsGet();
            DeviceFarm? farm = crop != null ? farms.FirstOrDefault(f => f.IDDeviceFarm == crop.FarmID) : null;

            IList<DeviceFleetStatus> devices = (await api.DeviceFleetGet())
                .Where(d => d.FarmParcelZoneID == idFarmParcelZone)
                .ToList();
            bool hasController = devices.Any(d => d.ControllerCapable);

            string? timeZone = User.GetTimeZone();
            return new ParcelViewModel
            {
                Parcel = parcel,
                Crop = crop,
                Farm = farm ?? new DeviceFarm(),
                Devices = devices,
                Rules = hasController ? await api.FarmParcelZoneRulesGet(idFarmParcelZone) : [],
                ManualOverrides = hasController ? await api.ParcelManualActuateStatus(idFarmParcelZone) : [],
                DisplayTimeZone = string.IsNullOrWhiteSpace(timeZone) ? "UTC" : timeZone,
                DiscoveredDevices = await api.DiscoveryResultsGet(null, null, idFarmParcelZone),
                WifiConfigs = await api.DiscoveryWifiConfigsGet(),
            };
        }

        // ---- Manual Actuate - Open-Field's equivalent of DeviceFarmUnitController's ZoneManualActuateStart/Stop ----

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ParcelManualActuateStart(int idFarmParcelZone, RelayFunction relayFunction, ManualOverrideMode mode,
            int? durationMinutes, SensorMetric? targetMetric, double? targetThreshold, double? targetHysteresis)
        {
            var request = new ManualActuateRequest(relayFunction, mode, durationMinutes is int m ? m * 60 : null, targetMetric, targetThreshold, targetHysteresis);
            try
            {
                await api.ParcelManualActuateStart(idFarmParcelZone, request);
                TempData["Message"] = $"{relayFunction} manually started.";
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Parcel), new { idFarmParcelZone });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ParcelManualActuateStop(int idFarmParcelZone, RelayFunction relayFunction)
        {
            try
            {
                await api.ParcelManualActuateStop(idFarmParcelZone, relayFunction);
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Parcel), new { idFarmParcelZone });
        }

        // ---- Device discovery - Open-Field's equivalent of DeviceFarmUnitController's ScanZone/RegisterDiscoveredDeviceZone ----

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ScanParcel(DiscoveryScanRequest request)
        {
            try
            {
                await api.DiscoveryScan(request);
                TempData["Message"] = "Scan started - discovered devices will appear here shortly.";
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Parcel), new { idFarmParcelZone = request.ParcelID });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RegisterDiscoveredDeviceParcel(DiscoveryRegisterRequest request)
        {
            try
            {
                DiscoveryRegisterResult result = await api.DiscoveryRegister(request);
                var (message, error) = DiscoveryRegisterOutcomeMessage.For(result.Outcome);
                TempData["Message"] = message;
                TempData["Error"] = error;
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Parcel), new { idFarmParcelZone = request.ParcelID });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ParcelRename(int idFarmParcelZone, string farmOpenfieldCropParcelName)
        {
            FarmParcelZone parcel = await api.ParcelGetById(idFarmParcelZone);
            parcel.FarmParcelZoneName = farmOpenfieldCropParcelName;
            await api.ParcelUpdate(parcel);
            return RedirectToAction(nameof(Parcel), new { idFarmParcelZone });
        }

        // All three null together means "no tank tracking", mirrors DeviceFarmUnitController.TankCalibrationUpdate exactly.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SafetyLimitsUpdate(int idFarmParcelZone, int? waterPumpMaxRunSeconds, int? waterPumpCooldownSeconds, bool skipWaterPumpWhenRainPredicted,
            int? heatingMaxRunSeconds, int? ventilationMaxRunSeconds, HeatingFailSafePolicyType? heatingFailSafePolicy,
            double? tankCapacityLiters, int? waterLevelRawEmpty, int? waterLevelRawFull, double? waterPumpMinLevel)
        {
            FarmParcelZone parcel = await api.ParcelGetById(idFarmParcelZone);
            parcel.WaterPumpMaxRunSeconds = waterPumpMaxRunSeconds;
            parcel.WaterPumpCooldownSeconds = waterPumpCooldownSeconds;
            parcel.SkipWaterPumpWhenRainPredicted = skipWaterPumpWhenRainPredicted;
            parcel.HeatingMaxRunSeconds = heatingMaxRunSeconds;
            parcel.VentilationMaxRunSeconds = ventilationMaxRunSeconds;
            parcel.HeatingFailSafePolicy = heatingFailSafePolicy;
            parcel.TankCapacityLiters = tankCapacityLiters;
            parcel.WaterLevelRawEmpty = waterLevelRawEmpty;
            parcel.WaterLevelRawFull = waterLevelRawFull;
            parcel.WaterPumpMinLevel = waterPumpMinLevel;
            try
            {
                await api.ParcelUpdate(parcel);
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Parcel), new { idFarmParcelZone });
        }

        // ---- Device assignment ---------------------------------------------

        [Authorize(Roles = RoleNames.DeviceManagersOrGlobalReader)]
        public async Task<ActionResult> ParcelAssignPicker(int idFarmParcelZone, bool controllerCapable) =>
            View(new ParcelAssignPickerViewModel
            {
                IDFarmParcelZone = idFarmParcelZone,
                ControllerCapable = controllerCapable,
                Devices = await api.DeviceUnassignedGet(controllerCapable),
            });

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Assign(int idDevice, int idFarmParcelZone, bool controllerCapable)
        {
            try
            {
                await api.ParcelDeviceAssign(new DeviceParcelAssignment { IDDevice = idDevice, IDFarmParcelZone = idFarmParcelZone });
            }
            catch (ApiException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Body);
                return View(nameof(ParcelAssignPicker), new ParcelAssignPickerViewModel
                {
                    IDFarmParcelZone = idFarmParcelZone,
                    ControllerCapable = controllerCapable,
                    Devices = await api.DeviceUnassignedGet(controllerCapable),
                });
            }
            return RedirectToAction(nameof(Parcel), new { idFarmParcelZone });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Remove(int idDevice, int idFarmParcelZone)
        {
            await api.ParcelDeviceUnassign(idDevice);
            return RedirectToAction(nameof(Parcel), new { idFarmParcelZone });
        }

        // ---- Rules (Crop/Parcel scope) - Crop's own page, mirrors DeviceFarmUnitController.UnitRules; Parcel's rules are embedded inline in Parcel.cshtml instead, mirroring the Zone page. ----

        [Authorize(Roles = RoleNames.DeviceManagersOrGlobalReader)]
        public async Task<ActionResult> SowingRules(int idSowing) => View(new RuleEditorViewModel
        {
            Scope = RuleScope.Crop,
            ScopeId = idSowing,
            Rules = await api.SowingRulesGet(idSowing),
            RedirectActionName = nameof(SowingRules),
        });

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SowingRuleAdd(int idSowing, RuleFormInput input)
        {
            await AddRuleAsync(BuildOpenfieldRule(input, idSowing: idSowing), r => api.SowingRuleAdd(r));
            return RedirectToAction(nameof(SowingRules), new { idSowing });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SowingRuleDelete(int idDeviceFarmUnitZoneRule, int idSowing)
        {
            await DeleteRuleAsync(idDeviceFarmUnitZoneRule, r => api.SowingRuleDelete(r));
            return RedirectToAction(nameof(SowingRules), new { idSowing });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> FarmParcelZoneRuleAdd(int idFarmParcelZone, RuleFormInput input)
        {
            await AddRuleAsync(BuildOpenfieldRule(input, idFarmParcelZone: idFarmParcelZone), r => api.FarmParcelZoneRuleAdd(r));
            return RedirectToAction(nameof(Parcel), new { idFarmParcelZone });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> FarmParcelZoneRuleDelete(int idDeviceFarmUnitZoneRule, int idFarmParcelZone)
        {
            await DeleteRuleAsync(idDeviceFarmUnitZoneRule, r => api.FarmParcelZoneRuleDelete(r));
            return RedirectToAction(nameof(Parcel), new { idFarmParcelZone });
        }

        /// Mirrors DeviceFarmUnitController.BuildRule - exactly one of idSowing/idFarmParcelZone is non-null. RootConditionJson comes pre-built from wwwroot/js/rule-builder.js, same as BuildRule.
        private static DeviceFarmUnitZoneRule BuildOpenfieldRule(RuleFormInput input, int? idSowing = null, int? idFarmParcelZone = null)
        {
            ConditionNode? root = string.IsNullOrWhiteSpace(input.RootConditionJson)
                ? null
                : System.Text.Json.JsonSerializer.Deserialize<ConditionNode>(input.RootConditionJson, ConditionConfigJson.Options);
            return new DeviceFarmUnitZoneRule
            {
                DeviceSowingID = idSowing,
                DeviceFarmParcelZoneID = idFarmParcelZone,
                ActionType = input.ActionType,
                RelayFunction = input.ActionType == ActionType.Relay ? input.RelayFunction : null,
                Name = input.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim(),
                TargetPercent = input.ActionType == ActionType.Relay ? input.TargetPercent : null,
                IsSafetyRule = input.IsSafetyRule,
                NotificationSubject = input.ActionType == ActionType.Notification ? input.NotificationSubject : null,
                NotificationBody = input.ActionType == ActionType.Notification ? input.NotificationBody : null,
                Root = root,
            };
        }

        private async Task AddRuleAsync(DeviceFarmUnitZoneRule rule, Func<DeviceFarmUnitZoneRule, Task<RuleAddResult>> add)
        {
            try
            {
                RuleAddResult result = await add(rule);
                if (result.ScopeConflictWarning != null)
                {
                    TempData["Warning"] = result.ScopeConflictWarning;
                }
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
        }

        private async Task DeleteRuleAsync(int idRule, Func<int?, Task> delete)
        {
            try
            {
                await delete(idRule);
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
        }
    }
}
