using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Devices;
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
        private sealed record EvalItem(DeviceFarmUnitZoneRule Rule, int ZoneId, int TenantId, bool WasTrue, SensorAverages? Averages, int UtcOffsetSeconds, SensorTrend? Trend);

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

            // Roadmap #398(2) - lets a Notification rule use a real sunrise/sunset window (e.g. "CO2 low" only during daytime) instead of a fixed Schedule approximation; same cascade/resolver DeviceConfigBuilder already uses for Relay rules (roadmap #396(6)), a rule that can't resolve today (no location set) is dropped, not sent through with a broken node.
            ServerConfig serverConfig = await serverConfigRepo.ServerConfigGetAsync(1);
            double? lat = tenant.Latitude ?? serverConfig.WeatherLocationLat;
            double? lon = tenant.Longitude ?? serverConfig.WeatherLocationLon;
            DateOnly localDate = DateOnly.FromDateTime(utcNow.AddSeconds(utcOffsetSeconds));
            notificationRules = AstronomicalRuleResolver.Resolve(notificationRules, lat, lon, localDate, utcOffsetSeconds);

            // Which zones (if any) currently have a member device of an active simulation session, and which session - fetched once per tenant, not once per zone. Simulation-scoped rules for each such session are also fetched lazily and cached here (a session commonly covers several zones).
            IDictionary<int, int> simulationSessionIdByZone = await simulationRepo.ActiveSimulationSessionIdsByZoneAsync(tenantId);
            var simulationRulesBySession = new Dictionary<int, List<DeviceFarmUnitZoneRule>>();

            // Same batched, once-per-tenant lookup as Simulation above (Zone>Unit>Farm cascade already resolved by ActiveExperimentIdsByZoneAsync itself).
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
                // Farm rules only apply when this Unit is actually assigned to one - a Farm-less Unit sees no Farm-scope rules (roadmap #384).
                var farmScoped = unit.DeviceFarmID is int unitFarmId
                    ? notificationRules.Where(r => r.DeviceFarmID == unitFarmId).ToList()
                    : [];
                // Also excludes a Crop/Parcel-scoped rule - without this, a rule meant for one Open-Field crop/parcel would leak into every Greenhouse zone's "Global" set, since it likewise has DeviceFarmID/DeviceFarmUnitID/DeviceFarmUnitZoneID all null.
                var globalScoped = notificationRules.Where(r => r.DeviceFarmID == null && r.DeviceFarmUnitID == null && r.DeviceFarmUnitZoneID == null
                    && r.DeviceSowingID == null && r.DeviceFarmParcelZoneID == null).ToList();

                foreach (DeviceFarmUnitZone zone in await unitRepo.DeviceFarmUnitZonesGetAsync(unitId))
                {
                    if (zone.IDDeviceFarmUnitZone is not int zoneId)
                    {
                        continue;
                    }
                    var zoneScoped = notificationRules.Where(r => r.DeviceFarmUnitZoneID == zoneId).ToList();
                    List<DeviceFarmUnitZoneRule> simulationScoped = [];
                    if (simulationSessionIdByZone.TryGetValue(zoneId, out int simSessionId))
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
                    foreach (DeviceFarmUnitZoneRule rule in effective)
                    {
                        if (rule.IDDeviceFarmUnitZoneRule is not int ruleId)
                        {
                            continue;
                        }
                        bool wasTrue = await unitRepo.RuleNotificationWasTrueGetAsync(ruleId, zoneId);
                        items.Add(new EvalItem(rule, zoneId, tenantId, wasTrue, dashboard?.Averages, utcOffsetSeconds, dashboard?.Trend));
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
                    List<DeviceFarmUnitZoneRule> simulationScoped = [];
                    if (simulationSessionIdByZone.TryGetValue(parcelId, out int simSessionId))
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
                    foreach (DeviceFarmUnitZoneRule rule in effective)
                    {
                        if (rule.IDDeviceFarmUnitZoneRule is not int ruleId)
                        {
                            continue;
                        }
                        bool wasTrue = await unitRepo.RuleNotificationWasTrueGetAsync(ruleId, parcelId);
                        items.Add(new EvalItem(rule, parcelId, tenantId, wasTrue, averages, utcOffsetSeconds, trend));
                    }
                }
            }

            if (items.Count == 0)
            {
                return;
            }

            // Fixed-point resolution for RuleTriggered chaining (roadmap #212): "referenced rule fired" means
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
                    Func<SensorMetric, double?> readMetric = metric => item.Averages != null ? ReadMetric(item.Averages, metric) : null;
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
            double? firstValue = firstComparison?.Metric is SensorMetric m && item.Averages != null ? ReadMetric(item.Averages, m) : null;
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

        private static double? ReadMetric(SensorAverages averages, SensorMetric metric) => metric switch
        {
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
