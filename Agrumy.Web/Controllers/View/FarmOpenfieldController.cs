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
    /// Sowing/FarmParcel/FarmParcelZone CRUD, the sjetva wizard's Start/Close lifecycle, device assignment, and safety-limit editing (D1/D2/D9) - the Open-Field mirror of DeviceFarmUnitController's Zone/Unit pages. Farm-level actions (Add/Rename/Delete/rule pages) stay on DeviceFarmUnitController, shared by both branches - this controller only owns what's genuinely new.
    [Authorize]
    public class FarmOpenfieldController(IApi api) : Controller
    {
        // ---- Root page (D1) --------------------------------------------

        /// The dedicated Open-Field root page - every Open-Field farm with its parcels/zones (occupancy shown per zone) and sowings, replacing the old mixed Farms page's crop-cube section.
        public async Task<ActionResult> Index()
        {
            IList<DeviceFarm> farms = (await api.DeviceFarmsGet()).Where(f => f.FarmType == FarmType.OpenField).ToList();
            IList<FarmOpenfield> openfields = await api.FarmOpenfieldsGet();
            IList<Sowing> sowings = await api.CropsGet();
            var farmModels = new List<FarmOpenfieldFarmViewModel>();
            foreach (DeviceFarm farm in farms)
            {
                FarmOpenfield? openfield = openfields.FirstOrDefault(o => o.FarmID == farm.IDDeviceFarm);
                var parcelModels = new List<FarmParcelWithZonesViewModel>();
                if (openfield?.IDFarmOpenfield is int idFarmOpenfield)
                {
                    foreach (FarmParcel parcel in await api.FarmParcelsGet(idFarmOpenfield))
                    {
                        parcelModels.Add(new FarmParcelWithZonesViewModel { Parcel = parcel, Zones = await api.FarmParcelZonesGet(parcel.IDFarmParcel!.Value) });
                    }
                }
                farmModels.Add(new FarmOpenfieldFarmViewModel
                {
                    Farm = farm,
                    Openfield = openfield,
                    Parcels = parcelModels,
                    Sowings = sowings.Where(s => s.FarmID == farm.IDDeviceFarm).ToList(),
                });
            }
            return View(new FarmOpenfieldIndexViewModel { Farms = farmModels });
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

        // ---- Sowing CRUD --------------------------------------------------

        /// Sjetva wizard step 1 (D3/D9): crop (looked up/created in the catalog server-side by name) + variety + start date + expected duration. Creates a Planned sowing with no zones occupied yet - step 2 (the zone picker) lives on the Sowing Details page below, since a freshly created sowing has no zones of its own to show.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CropAdd(int idFarm, string farmOpenfieldCropName, string? variety, DateOnly startDate, int expectedDurationDays)
        {
            Sowing added = await api.CropAdd(new Sowing
            {
                FarmID = idFarm,
                SowingName = farmOpenfieldCropName,
                Variety = variety,
                StartDate = startDate,
                ExpectedDurationDays = expectedDurationDays,
            });
            return RedirectToAction(nameof(Parcels), new { idSowing = added.IDSowing });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CropRename(int idSowing, string farmOpenfieldCropName)
        {
            Sowing crop = await api.CropGet(idSowing);
            crop.SowingName = farmOpenfieldCropName;
            await api.CropUpdate(crop);
            return RedirectToAction(nameof(Parcels), new { idSowing });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CropDelete(int idSowing)
        {
            await api.CropDelete(idSowing);
            return RedirectToAction(nameof(Index));
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
            if (crop.Status == GrowingCycleStatus.Planned)
            {
                IList<FarmOpenfield> openfields = await api.FarmOpenfieldsGet();
                if (openfields.FirstOrDefault(o => o.FarmID == crop.FarmID)?.IDFarmOpenfield is int idFarmOpenfield)
                {
                    foreach (FarmParcel parcel in await api.FarmParcelsGet(idFarmOpenfield))
                    {
                        availableParcels.Add(new FarmParcelWithZonesViewModel { Parcel = parcel, Zones = await api.FarmParcelZonesGet(parcel.IDFarmParcel!.Value) });
                    }
                }
            }

            return View(new CropParcelsViewModel
            {
                Crop = crop,
                Farm = farm ?? new DeviceFarm(),
                Parcels = await api.ParcelDashboardListGet(idSowing),
                AvailableParcels = availableParcels,
                LogEntries = await api.FieldLogEntriesGet(idSowing),
                EarliestHarvestDate = await api.EarliestHarvestDateGet(idSowing),
                NitrogenBalanceKgPerHa = await api.NitrogenBalanceGet(idSowing),
            });
        }

        // ---- FarmParcel / FarmParcelZone CRUD (D2/D3/D4) -----------------------------------

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> FarmParcelAdd(int idFarmOpenfield, string farmParcelName)
        {
            await api.FarmParcelAdd(idFarmOpenfield, farmParcelName);
            return RedirectToAction(nameof(Index));
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
            return RedirectToAction(nameof(Index));
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
            return RedirectToAction(nameof(Index));
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
