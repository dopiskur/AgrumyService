using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Devices;
using Agrumy.Api.Weather;
using Agrumy.Rules;
using Agrumy.Shared.Models;
using Agrumy.Api.Notifications;
using Agrumy.Shared.Utils;

namespace Agrumy.Api.BackgroundWorkers
{
    /// Evaluates every Notification-action rule against each zone it reaches (Simulation>Experiment>Zone>Unit>Farm>Global
    /// precedence resolved per zone via RuleHierarchyResolver.ResolveNotificationRules, same "more specific
    /// wins" semantics as Relay rules), dispatching one notification per false->true transition - a Relay
    /// rule's OR-across-rules/AND-OR-within-a-rule fold happens on-device, this is the server-side
    /// equivalent for the action type firmware has no way to perform itself.
    public sealed class RuleNotificationEvaluator(
        ITenantRepository tenantRepo, IDeviceFarmUnitRepository unitRepo, ISowingRepository sowingRepo, IFarmParcelRepository farmParcelRepo, IUserRepository userRepo,
        INotificationDispatcher dispatcher, IServerConfigRepository serverConfigRepo, ISimulationRepository simulationRepo,
        IExperimentRepository experimentRepo)
    {
        private sealed record EvalItem(DeviceFarmUnitZoneRule Rule, int ZoneId, int TenantId, bool WasTrue, SensorAverages? Averages, int UtcOffsetSeconds, SensorTrend? Trend, WeatherLocationState WeatherState);

        public async Task RunOnceAsync(CancellationToken ct = default)
        {
            foreach (Tenant tenant in await tenantRepo.TenantsGetAllAsync())
            {
                ct.ThrowIfCancellationRequested();
                if (tenant.IDTenant is not int tenantId)
                {
                    continue;
                }

                IList<DeviceFarmUnitZoneRule> notificationRules = await unitRepo.RulesGetNotificationRulesForTenantAsync(tenantId);
                if (notificationRules.Count == 0)
                {
                    continue;
                }

                await EvaluateTenantAsync(tenant, tenantId, notificationRules, ct);
            }
        }

        private async Task EvaluateTenantAsync(Tenant tenant, int tenantId, IList<DeviceFarmUnitZoneRule> notificationRules, CancellationToken ct)
        {
            int utcOffsetSeconds = TimeZoneHelper.GetUtcOffsetSeconds(DateTime.UtcNow, tenant.ScheduleTimeZone);
            DateTime utcNow = DateTime.UtcNow;

            // Lets a Notification rule use a real sunrise/sunset window (e.g. "CO2 low" only during daytime) instead of a fixed Schedule approximation; same cascade/resolver DeviceConfigBuilder already uses for Relay rules, a rule that can't resolve today (no location set) is dropped, not sent through with a broken node.
            ServerConfig serverConfig = await serverConfigRepo.ServerConfigGetAsync(1);
            double? lat = tenant.Latitude ?? serverConfig.WeatherLocationLat;
            double? lon = tenant.Longitude ?? serverConfig.WeatherLocationLon;
            DateOnly localDate = DateOnly.FromDateTime(utcNow.AddSeconds(utcOffsetSeconds));
            notificationRules = AstronomicalRuleResolver.Resolve(notificationRules, lat, lon, localDate, utcOffsetSeconds);

            // Feeds SensorMetric.OutdoorTemperature/OutdoorHumidity/OutdoorWind/OutdoorPressure below - a zone/parcel resolves to its own Unit/Farm/parcel pin (WeatherLocationResolver) when it has one, so two sites in the same organization no longer share one reading; cached per resolved point so a farm/unit shared by many zones is looked up once, not once per zone.
            var weatherStateCache = new Dictionary<(double, double), WeatherLocationState>();
            async Task<WeatherLocationState> WeatherStateForAsync((double Lat, double Lon)? location)
            {
                if (location is not (double lat2, double lon2))
                {
                    return new WeatherLocationState { TenantID = tenantId };
                }
                if (!weatherStateCache.TryGetValue((lat2, lon2), out WeatherLocationState? cached))
                {
                    cached = await tenantRepo.WeatherLocationStateGetAsync(tenantId, lat2, lon2);
                    weatherStateCache[(lat2, lon2)] = cached;
                }
                return cached;
            }

            // A zone under an active Simulation session can have its own "simulate OpenWeather" override (a virtual device's DeviceSimulation) - only the four Outdoor* readings are ever overridden this way, never WeatherRainPredicted/FrostPredicted (those stay real, same as every other rule not covered by the session).
            var weatherOverrideCache = new Dictionary<int, OutdoorConditions?>();
            async Task<WeatherLocationState> EffectiveWeatherStateAsync((double Lat, double Lon)? location, int? simSessionId)
            {
                WeatherLocationState baseState = await WeatherStateForAsync(location);
                if (simSessionId is not int sid)
                {
                    return baseState;
                }
                if (!weatherOverrideCache.TryGetValue(sid, out OutdoorConditions? sim))
                {
                    sim = await simulationRepo.SimulationSessionWeatherOverrideGetAsync(sid);
                    weatherOverrideCache[sid] = sim;
                }
                if (sim == null)
                {
                    return baseState;
                }
                return new WeatherLocationState
                {
                    TenantID = baseState.TenantID,
                    Latitude = baseState.Latitude,
                    Longitude = baseState.Longitude,
                    WeatherRainPredicted = baseState.WeatherRainPredicted,
                    WeatherCheckedAtUtc = baseState.WeatherCheckedAtUtc,
                    FrostPredicted = baseState.FrostPredicted,
                    FrostPredictedHoursAhead = baseState.FrostPredictedHoursAhead,
                    FrostCheckedAtUtc = baseState.FrostCheckedAtUtc,
                    OutdoorTemperatureC = sim.TemperatureC ?? baseState.OutdoorTemperatureC,
                    OutdoorHumidityPercent = sim.HumidityPercent ?? baseState.OutdoorHumidityPercent,
                    OutdoorWindSpeedMetersPerSecond = sim.WindSpeedMetersPerSecond ?? baseState.OutdoorWindSpeedMetersPerSecond,
                    OutdoorPressureHpa = sim.PressureHpa ?? baseState.OutdoorPressureHpa,
                    OutdoorCheckedAtUtc = baseState.OutdoorCheckedAtUtc,
                };
            }

            IList<DeviceFarm> farms = await unitRepo.DeviceFarmsGetAsync(tenantId);
            Dictionary<int, DeviceFarm> farmsById = farms.Where(f => f.IDDeviceFarm is int).ToDictionary(f => f.IDDeviceFarm!.Value);
            var parcelsById = new Dictionary<int, FarmParcel>();
            async Task<FarmParcel?> ParcelForAsync(int idFarmParcel)
            {
                if (!parcelsById.TryGetValue(idFarmParcel, out FarmParcel? parcel))
                {
                    parcel = await farmParcelRepo.FarmParcelGetByIdAsync(idFarmParcel);
                    if (parcel != null)
                    {
                        parcelsById[idFarmParcel] = parcel;
                    }
                }
                return parcel;
            }

            // Which zones (if any) currently have a member device of an active simulation session, and which session - fetched once per organization, not once per zone. Simulation-scoped rules for each such session are also fetched lazily and cached here (a session commonly covers several zones).
            IDictionary<int, int> simulationSessionIdByZone = await simulationRepo.ActiveSimulationSessionIdsByZoneAsync(tenantId);
            var simulationRulesBySession = new Dictionary<int, List<DeviceFarmUnitZoneRule>>();

            // Same batched, once-per-organization lookup as Simulation above (Zone>Unit>Farm cascade already resolved by ActiveExperimentIdsByZoneAsync itself).
            IDictionary<int, int> experimentIdByZone = await experimentRepo.ActiveExperimentIdsByZoneAsync(tenantId);
            var experimentRulesByExperiment = new Dictionary<int, List<DeviceFarmUnitZoneRule>>();

            var items = new List<EvalItem>();
            foreach (DeviceFarmUnit unit in await unitRepo.DeviceFarmUnitsGetAsync(tenantId))
            {
                if (unit.IDDeviceFarmUnit is not int unitId)
                {
                    continue;
                }
                var unitScoped = notificationRules.Where(r => r.DeviceFarmUnitID == unitId).ToList();
                // Farm rules only apply when this Unit is actually assigned to one - a Farm-less Unit sees no Farm-scope rules.
                var farmScoped = unit.DeviceFarmID is int unitFarmId
                    ? notificationRules.Where(r => r.DeviceFarmID == unitFarmId).ToList()
                    : [];
                // Also excludes an Arable/Parcel-scoped rule - without this, a rule meant for one Open-Field crop/parcel would leak into every Greenhouse zone's "Global" set, since it likewise has DeviceFarmID/DeviceFarmUnitID/DeviceFarmUnitZoneID all null.
                var globalScoped = notificationRules.Where(r => r.DeviceFarmID == null && r.DeviceFarmUnitID == null && r.DeviceFarmUnitZoneID == null
                    && r.DeviceSowingID == null && r.DeviceFarmParcelZoneID == null).ToList();

                foreach (DeviceFarmUnitZone zone in await unitRepo.DeviceFarmUnitZonesGetAsync(unitId))
                {
                    if (zone.IDDeviceFarmUnitZone is not int zoneId)
                    {
                        continue;
                    }
                    var zoneScoped = notificationRules.Where(r => r.DeviceFarmUnitZoneID == zoneId).ToList();
                    bool hasSimulation = simulationSessionIdByZone.TryGetValue(zoneId, out int simSessionId);
                    List<DeviceFarmUnitZoneRule> simulationScoped = [];
                    if (hasSimulation)
                    {
                        if (!simulationRulesBySession.TryGetValue(simSessionId, out List<DeviceFarmUnitZoneRule>? cached))
                        {
                            cached = (await unitRepo.RulesGetForSimulationAsync(simSessionId)).Where(r => r.ActionType == ActionType.Notification).ToList();
                            simulationRulesBySession[simSessionId] = cached;
                        }
                        simulationScoped = cached;
                    }
                    List<DeviceFarmUnitZoneRule> experimentScoped = [];
                    if (experimentIdByZone.TryGetValue(zoneId, out int experimentId))
                    {
                        if (!experimentRulesByExperiment.TryGetValue(experimentId, out List<DeviceFarmUnitZoneRule>? cachedExperiment))
                        {
                            cachedExperiment = (await unitRepo.RulesGetForExperimentAsync(experimentId)).Where(r => r.ActionType == ActionType.Notification).ToList();
                            experimentRulesByExperiment[experimentId] = cachedExperiment;
                        }
                        experimentScoped = cachedExperiment;
                    }
                    IList<DeviceFarmUnitZoneRule> effective = RuleHierarchyResolver.ResolveNotificationRules(simulationScoped, experimentScoped, zoneScoped, unitScoped, farmScoped, globalScoped);
                    if (effective.Count == 0)
                    {
                        continue;
                    }

                    DeviceFarmUnitZoneDashboard? dashboard = await unitRepo.DeviceFarmUnitZoneDashboardGetAsync(zoneId);
                    DeviceFarm? farm = unit.DeviceFarmID is int farmId2 && farmsById.TryGetValue(farmId2, out DeviceFarm? f2) ? f2 : null;
                    WeatherLocationState zoneWeatherState = await EffectiveWeatherStateAsync(WeatherLocationResolver.ForGreenhouseUnit(unit, farm, tenant, serverConfig), hasSimulation ? simSessionId : null);
                    foreach (DeviceFarmUnitZoneRule rule in effective)
                    {
                        if (rule.IDDeviceFarmUnitZoneRule is not int ruleId)
                        {
                            continue;
                        }
                        bool wasTrue = await unitRepo.RuleNotificationWasTrueGetAsync(ruleId, zoneId);
                        items.Add(new EvalItem(rule, zoneId, tenantId, wasTrue, dashboard?.Averages, utcOffsetSeconds, dashboard?.Trend, zoneWeatherState));
                    }
                }
            }

            // Open-Field's own Farm>Sowing>FarmParcelZone walk (restructure R, D5), mirrors the Unit>Zone loop above field-for-field - a sowing's occupied-zone list is already empty for one that never started (Planned) or has closed, so D10's "no active sowing = no rules" falls out naturally without extra filtering. simulationSessionIdByZone/experimentIdByZone already cover FarmParcelZone ids too (see ActiveSimulationSessionIdsByZoneAsync/ActiveExperimentIdsByZoneAsync), and the *ScopedByExperiment/BySession caches above are shared across both branches since they're keyed by session/experiment id, not by zone.
            foreach (Sowing sowing in await sowingRepo.SowingsGetAsync(tenantId))
            {
                if (sowing.IDSowing is not int sowingId)
                {
                    continue;
                }
                var sowingScoped = notificationRules.Where(r => r.DeviceSowingID == sowingId).ToList();
                var farmScoped = notificationRules.Where(r => r.DeviceFarmID == sowing.FarmID).ToList();
                var globalScoped = notificationRules.Where(r => r.DeviceFarmID == null && r.DeviceFarmUnitID == null && r.DeviceFarmUnitZoneID == null
                    && r.DeviceSowingID == null && r.DeviceFarmParcelZoneID == null).ToList();

                foreach (FarmParcelZone parcel in await sowingRepo.SowingOccupiedZonesGetAsync(sowingId))
                {
                    if (parcel.IDFarmParcelZone is not int parcelId)
                    {
                        continue;
                    }
                    var parcelScoped = notificationRules.Where(r => r.DeviceFarmParcelZoneID == parcelId).ToList();
                    bool hasSimulation = simulationSessionIdByZone.TryGetValue(parcelId, out int simSessionId);
                    List<DeviceFarmUnitZoneRule> simulationScoped = [];
                    if (hasSimulation)
                    {
                        if (!simulationRulesBySession.TryGetValue(simSessionId, out List<DeviceFarmUnitZoneRule>? cached))
                        {
                            cached = (await unitRepo.RulesGetForSimulationAsync(simSessionId)).Where(r => r.ActionType == ActionType.Notification).ToList();
                            simulationRulesBySession[simSessionId] = cached;
                        }
                        simulationScoped = cached;
                    }
                    List<DeviceFarmUnitZoneRule> experimentScoped = [];
                    if (experimentIdByZone.TryGetValue(parcelId, out int experimentId))
                    {
                        if (!experimentRulesByExperiment.TryGetValue(experimentId, out List<DeviceFarmUnitZoneRule>? cachedExperiment))
                        {
                            cachedExperiment = (await unitRepo.RulesGetForExperimentAsync(experimentId)).Where(r => r.ActionType == ActionType.Notification).ToList();
                            experimentRulesByExperiment[experimentId] = cachedExperiment;
                        }
                        experimentScoped = cachedExperiment;
                    }
                    IList<DeviceFarmUnitZoneRule> effective = RuleHierarchyResolver.ResolveNotificationRules(simulationScoped, experimentScoped, parcelScoped, sowingScoped, farmScoped, globalScoped);
                    if (effective.Count == 0)
                    {
                        continue;
                    }

                    (SensorAverages averages, SensorTrend trend) = await farmParcelRepo.FarmParcelZoneAggregateAsync(parcelId);
                    FarmParcel? parcelContainer = await ParcelForAsync(parcel.FarmParcelID);
                    WeatherLocationState parcelWeatherState = await EffectiveWeatherStateAsync(WeatherLocationResolver.ForParcelZone(parcel, parcelContainer, tenant, serverConfig), hasSimulation ? simSessionId : null);
                    foreach (DeviceFarmUnitZoneRule rule in effective)
                    {
                        if (rule.IDDeviceFarmUnitZoneRule is not int ruleId)
                        {
                            continue;
                        }
                        bool wasTrue = await unitRepo.RuleNotificationWasTrueGetAsync(ruleId, parcelId);
                        items.Add(new EvalItem(rule, parcelId, tenantId, wasTrue, averages, utcOffsetSeconds, trend, parcelWeatherState));
                    }
                }
            }

            if (items.Count == 0)
            {
                return;
            }

            // Fixed-point resolution for RuleTriggered chaining: "referenced rule fired" means
            // "fired in ANY zone it applies to, this tick" - a deliberate simplification, not per-zone dependency
            // tracking. firedThisTick only grows, so repeating settles within a bounded number of rounds; a
            // dependency that never resolves (missing/circular reference) just evaluates false for that
            // condition, same as any other unmet condition.
            var firedThisTick = new HashSet<int>();
            var results = new Dictionary<EvalItem, bool>();
            for (int round = 0; round < 10; round++)
            {
                ct.ThrowIfCancellationRequested();
                int before = firedThisTick.Count;
                foreach (EvalItem item in items)
                {
                    Func<SensorMetric, double?> readMetric = metric => ReadMetric(item.Averages, metric, item.WeatherState);
                    bool result = RuleConditionEvaluator.EvaluateRule(item.Rule, item.WasTrue, readMetric, utcNow, item.UtcOffsetSeconds, firedThisTick.Contains, item.Trend);
                    results[item] = result;
                    if (result)
                    {
                        firedThisTick.Add(item.Rule.IDDeviceFarmUnitZoneRule!.Value);
                    }
                }
                if (firedThisTick.Count == before)
                {
                    break; // stable - no new rule fired this round, further rounds would repeat the same result
                }
            }

            foreach (EvalItem item in items)
            {
                await FinalizeAsync(item, results[item], ct);
            }
        }

        private async Task FinalizeAsync(EvalItem item, bool result, CancellationToken ct)
        {
            int ruleId = item.Rule.IDDeviceFarmUnitZoneRule!.Value;
            if (result == item.WasTrue)
            {
                return; // no transition - dedup, nothing to persist or notify
            }
            await unitRepo.RuleNotificationWasTrueSetAsync(ruleId, item.ZoneId, result, result ? DateTime.UtcNow : null);
            if (!result)
            {
                return; // true->false recovery clears the latch silently, no notification
            }

            var recipients = await NotificationRecipientBuilder.BuildForTenantAdminsAsync(userRepo, item.TenantId, NotificationEventType.RuleTriggered);
            // Best-effort now that a rule can span several metrics - {metric}/{value} resolve from the first ComparisonNode found in the tree, not "the" rule's metric (there no longer is a single one).
            ConditionNode? firstComparison = RuleConditionEvaluator.FindFirstComparison(item.Rule.Root);
            double? firstValue = firstComparison?.Metric is SensorMetric m ? ReadMetric(item.Averages, m, item.WeatherState) : null;
            if (recipients.Count > 0)
            {
                await dispatcher.DispatchToRecipientsAsync(
                    Placeholder(item.Rule.NotificationSubject) ?? "Agrumy alert",
                    Placeholder(item.Rule.NotificationBody) ?? string.Empty,
                    NotificationSeverity.Warning,
                    recipients,
                    ct: ct);
            }

            string? Placeholder(string? template) => template?
                .Replace("{value}", firstValue?.ToString("0.##") ?? "n/a")
                .Replace("{metric}", firstComparison?.Metric?.ToString() ?? "");
        }

        /// Outdoor* metrics read from that zone's own resolved-location WeatherEvaluator state regardless of averages (a zone with no reporting device can still have a working outdoor-weather rule); every other metric is a zone SensorAverages reading and needs one.
        private static double? ReadMetric(SensorAverages? averages, SensorMetric metric, WeatherLocationState weatherState) => metric switch
        {
            SensorMetric.OutdoorTemperature => weatherState.OutdoorTemperatureC,
            SensorMetric.OutdoorHumidity => weatherState.OutdoorHumidityPercent,
            SensorMetric.OutdoorWind => weatherState.OutdoorWindSpeedMetersPerSecond,
            SensorMetric.OutdoorPressure => weatherState.OutdoorPressureHpa,
            _ when averages == null => null,
            SensorMetric.Temperature => averages.Temperature,
            SensorMetric.SoilTemperature => averages.SoilTemperature,
            SensorMetric.Humidity => averages.Humidity,
            SensorMetric.Vpd => averages.Vpd,
            SensorMetric.DewPoint => averages.DewPoint,
            SensorMetric.DewPointSpread => averages.DewPointSpread,
            SensorMetric.Moisture => averages.Moisture,
            SensorMetric.Light => averages.Light,
            SensorMetric.Co2 => averages.Co2,
            SensorMetric.Tvoc => averages.Tvoc,
            SensorMetric.Barometer => averages.Barometer,
            SensorMetric.LiquidPH => averages.LiquidPH,
            SensorMetric.RainLevel => averages.RainLevel,
            SensorMetric.WaterLevel => averages.WaterLevel,
            SensorMetric.Wind => averages.Wind,
            SensorMetric.Ec => averages.Ec,
            SensorMetric.Weight => averages.Weight,
            _ => null,
        };
    }
}
