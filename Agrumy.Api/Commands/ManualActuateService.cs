using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;

namespace Agrumy.Api.Commands
{
    public enum ManualActuateOutcome
    {
        Success,
        TargetNotFound,
        UnsupportedFunction,
        InvalidTargetMetric,
        MissingMaxRunSeconds,
    }

    public sealed record ManualActuateResult(ManualActuateOutcome Outcome, IReadOnlyList<int> AffectedDeviceIds, string? Message = null);

    /// Roadmap #219 - target resolution/fan-out (a Zone's one controller, or every controller across a Unit's zones), validation, and the ExpiresAtUtc safety-cap math; no background worker, DeviceConfigBuilder reads the resulting rows lazily on each device's next poll.
    public sealed class ManualActuateService(IDeviceFarmUnitRepository unitRepo, IFarmParcelRepository farmParcelRepo)
    {
        /// Heating->Temperature only, Ventilation->Temperature or Humidity, WaterPump->Moisture (soil moisture - see AgrumyFirmware's sensor_analog_moist) only - roadmap #219's explicit per-function allowed subset.
        private static readonly Dictionary<RelayFunction, SensorMetric[]> AllowedTargetMetrics = new()
        {
            [RelayFunction.Heating] = [SensorMetric.Temperature],
            [RelayFunction.Ventilation] = [SensorMetric.Temperature, SensorMetric.Humidity],
            [RelayFunction.WaterPump] = [SensorMetric.Moisture],
        };

        private static int? MaxRunSecondsForFunction(IFarmLeafLevelNode leaf, RelayFunction function) => function switch
        {
            RelayFunction.Heating => leaf.HeatingMaxRunSeconds,
            RelayFunction.Ventilation => leaf.VentilationMaxRunSeconds,
            RelayFunction.WaterPump => leaf.WaterPumpMaxRunSeconds,
            _ => null,
        };

        public async Task<ManualActuateResult> StartForZoneAsync(int idDeviceFarmUnitZone, ManualActuateRequest request)
        {
            Device? controller = await unitRepo.DeviceFarmUnitZoneGetControllerAsync(idDeviceFarmUnitZone);
            if (controller?.IDDevice is not int deviceId)
            {
                return new ManualActuateResult(ManualActuateOutcome.TargetNotFound, [], $"Zone {idDeviceFarmUnitZone} has no controller assigned.");
            }
            DeviceFarmUnitZone? zone = await unitRepo.DeviceFarmUnitZoneGetByIdAsync(idDeviceFarmUnitZone);
            if (zone == null)
            {
                return new ManualActuateResult(ManualActuateOutcome.TargetNotFound, [], $"Zone {idDeviceFarmUnitZone} not found.");
            }
            return await StartForTargetsAsync([(deviceId, zone)], request, unitRepo.DeviceFarmUnitZoneConfigVersionBumpAsync);
        }

        /// Open-Field's equivalent of StartForZoneAsync - a parcel has at most one controller, same cap as a zone.
        public async Task<ManualActuateResult> StartForParcelAsync(int idFarmParcelZone, ManualActuateRequest request)
        {
            Device? controller = await farmParcelRepo.FarmParcelZoneGetControllerAsync(idFarmParcelZone);
            if (controller?.IDDevice is not int deviceId)
            {
                return new ManualActuateResult(ManualActuateOutcome.TargetNotFound, [], $"Parcel {idFarmParcelZone} has no controller assigned.");
            }
            FarmParcelZone? parcel = await farmParcelRepo.FarmParcelZoneGetByIdAsync(idFarmParcelZone);
            if (parcel == null)
            {
                return new ManualActuateResult(ManualActuateOutcome.TargetNotFound, [], $"Parcel {idFarmParcelZone} not found.");
            }
            return await StartForTargetsAsync([(deviceId, parcel)], request, farmParcelRepo.FarmParcelZoneConfigVersionBumpAsync);
        }

        /// Fans out to every zone under the unit that has a controller - a zone with no controller is simply skipped, not an error (same "absent zones are fine" reasoning as DeviceOutboxService's Unit fan-out for ScanForDevices).
        public async Task<ManualActuateResult> StartForUnitAsync(int idDeviceFarmUnit, ManualActuateRequest request)
        {
            IList<Device> controllers = await unitRepo.DeviceFarmUnitGetControllersAsync(idDeviceFarmUnit);
            if (controllers.Count == 0)
            {
                return new ManualActuateResult(ManualActuateOutcome.TargetNotFound, [], $"Unit {idDeviceFarmUnit} has no controllers across any of its zones.");
            }
            var targets = new List<(int DeviceId, IFarmLeafLevelNode Leaf)>();
            foreach (Device controller in controllers)
            {
                if (controller.IDDevice is not int deviceId || controller.DeviceFarmUnitZoneID is not int idZone)
                {
                    continue;
                }
                DeviceFarmUnitZone? zone = await unitRepo.DeviceFarmUnitZoneGetByIdAsync(idZone);
                if (zone != null)
                {
                    targets.Add((deviceId, zone));
                }
            }
            if (targets.Count == 0)
            {
                return new ManualActuateResult(ManualActuateOutcome.TargetNotFound, [], $"Unit {idDeviceFarmUnit} has no controllers across any of its zones.");
            }
            return await StartForTargetsAsync(targets, request, unitRepo.DeviceFarmUnitZoneConfigVersionBumpAsync);
        }

        /// Fans out to every controller across every unit/zone under the farm - same "absent zones/units are fine" reasoning as StartForUnitAsync.
        public async Task<ManualActuateResult> StartForFarmAsync(int idDeviceFarm, ManualActuateRequest request)
        {
            IList<Device> controllers = await unitRepo.DeviceFarmGetControllersAsync(idDeviceFarm);
            if (controllers.Count == 0)
            {
                return new ManualActuateResult(ManualActuateOutcome.TargetNotFound, [], $"Farm {idDeviceFarm} has no controllers across any of its units.");
            }
            var targets = new List<(int DeviceId, IFarmLeafLevelNode Leaf)>();
            foreach (Device controller in controllers)
            {
                if (controller.IDDevice is not int deviceId || controller.DeviceFarmUnitZoneID is not int idZone)
                {
                    continue;
                }
                DeviceFarmUnitZone? zone = await unitRepo.DeviceFarmUnitZoneGetByIdAsync(idZone);
                if (zone != null)
                {
                    targets.Add((deviceId, zone));
                }
            }
            if (targets.Count == 0)
            {
                return new ManualActuateResult(ManualActuateOutcome.TargetNotFound, [], $"Farm {idDeviceFarm} has no controllers across any of its units.");
            }
            return await StartForTargetsAsync(targets, request, unitRepo.DeviceFarmUnitZoneConfigVersionBumpAsync);
        }

        public async Task StopAsync(int idDeviceFarmUnitZone, RelayFunction relayFunction)
        {
            Device? controller = await unitRepo.DeviceFarmUnitZoneGetControllerAsync(idDeviceFarmUnitZone);
            if (controller?.IDDevice is int deviceId)
            {
                await unitRepo.ManualOverrideStopAsync(deviceId, relayFunction);
                await unitRepo.DeviceFarmUnitZoneConfigVersionBumpAsync(idDeviceFarmUnitZone);
            }
        }

        public async Task StopForParcelAsync(int idFarmParcelZone, RelayFunction relayFunction)
        {
            Device? controller = await farmParcelRepo.FarmParcelZoneGetControllerAsync(idFarmParcelZone);
            if (controller?.IDDevice is int deviceId)
            {
                await unitRepo.ManualOverrideStopAsync(deviceId, relayFunction);
                await farmParcelRepo.FarmParcelZoneConfigVersionBumpAsync(idFarmParcelZone);
            }
        }

        private async Task<ManualActuateResult> StartForTargetsAsync(List<(int DeviceId, IFarmLeafLevelNode Leaf)> targets, ManualActuateRequest request, Func<int, Task> bumpConfigVersionAsync)
        {
            if (!AllowedTargetMetrics.TryGetValue(request.RelayFunction, out SensorMetric[]? allowedMetrics))
            {
                return new ManualActuateResult(ManualActuateOutcome.UnsupportedFunction, [], $"{request.RelayFunction} cannot be manually actuated.");
            }
            if (request.Mode == ManualOverrideMode.Target && (request.TargetMetric is not SensorMetric metric || !allowedMetrics.Contains(metric)))
            {
                return new ManualActuateResult(ManualActuateOutcome.InvalidTargetMetric, [],
                    $"{request.RelayFunction} Target mode only accepts {string.Join(" or ", allowedMetrics)}.");
            }

            DateTime utcNow = DateTime.UtcNow;
            var affected = new List<int>();
            var zonesToBump = new HashSet<int>();
            bool anySkippedForMissingCap = false;

            // Runs the full loop regardless of a per-target skip - a Unit-level fan-out must not abandon zones already processed (and their pending ConfigVersion bump below) just because a LATER zone in the same batch lacks Target mode's required MaxRunSeconds.
            foreach (var (deviceId, leaf) in targets)
            {
                int? maxRunSeconds = MaxRunSecondsForFunction(leaf, request.RelayFunction);
                DateTime expiresAtUtc;
                if (request.Mode == ManualOverrideMode.Duration)
                {
                    int requestedSeconds = Math.Max(1, request.DurationSeconds ?? 0);
                    int cappedSeconds = maxRunSeconds is int cap && cap > 0 ? Math.Min(requestedSeconds, cap) : requestedSeconds;
                    expiresAtUtc = utcNow.AddSeconds(cappedSeconds);
                }
                else
                {
                    // Target mode has no natural self-cap (a broken/blocked sensor could otherwise run it forever) - MaxRunSeconds is mandatory here, not just an optional extra ceiling like it is for Duration mode.
                    if (maxRunSeconds is not int cap || cap <= 0)
                    {
                        anySkippedForMissingCap = true;
                        continue;
                    }
                    expiresAtUtc = utcNow.AddSeconds(cap);
                }

                await unitRepo.ManualOverrideStartAsync(new DeviceManualOverride
                {
                    DeviceID = deviceId,
                    TenantID = leaf.TenantID ?? 0,
                    RelayFunction = request.RelayFunction,
                    Mode = request.Mode,
                    StartedAtUtc = utcNow,
                    ExpiresAtUtc = expiresAtUtc,
                    TargetMetric = request.Mode == ManualOverrideMode.Target ? request.TargetMetric : null,
                    TargetThreshold = request.Mode == ManualOverrideMode.Target ? request.TargetThreshold : null,
                    TargetHysteresis = request.Mode == ManualOverrideMode.Target ? request.TargetHysteresis : null,
                });
                affected.Add(deviceId);
                if (leaf.Id is int idLeaf)
                {
                    zonesToBump.Add(idLeaf);
                }
            }

            if (affected.Count == 0 && anySkippedForMissingCap)
            {
                return new ManualActuateResult(ManualActuateOutcome.MissingMaxRunSeconds, [],
                    $"{request.RelayFunction} needs a MaxRunSeconds safety limit configured before Target mode can be used - none of the targeted zone(s) have one set.");
            }

            foreach (int idZone in zonesToBump)
            {
                await bumpConfigVersionAsync(idZone);
            }

            return new ManualActuateResult(ManualActuateOutcome.Success, affected,
                anySkippedForMissingCap ? $"Started on {affected.Count} of {targets.Count} zone(s) - the rest have no MaxRunSeconds configured for {request.RelayFunction} Target mode." : null);
        }
    }
}
