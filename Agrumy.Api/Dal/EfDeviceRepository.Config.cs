using Agrumy.Dal;
using Agrumy.Shared;
using Agrumy.Dal.Entities;
using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Firmware;
using Agrumy.Api.Quota;
using Agrumy.Api.Security;
using Agrumy.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace Agrumy.Api.Dal
{
    internal sealed partial class EfDeviceRepository
    {
        // ---- Per-device sensor/controller config -----------------------

        public async Task<DeviceConfigSensor?> DeviceConfigSensorGetAsync(int? deviceConfigSensorID)
        {
            var row = await db.DeviceConfigSensors.AsNoTracking()
                .FirstOrDefaultAsync(c => c.IDDeviceConfigSensor == deviceConfigSensorID);
            return row == null ? null : ToDto(row);
        }

        public async Task<DeviceConfigController?> DeviceConfigControllerGetAsync(int? deviceConfigControllerID)
        {
            var row = await db.DeviceConfigControllers.AsNoTracking()
                .FirstOrDefaultAsync(c => c.IDDeviceConfigController == deviceConfigControllerID);
            if (row == null)
            {
                return null;
            }
            IList<DeviceRelaySlot> relays = await db.DeviceConfigControllerRelays.AsNoTracking()
                .Where(r => r.IDDeviceConfigController == deviceConfigControllerID)
                .Select(r => new DeviceRelaySlot
                {
                    Slot = r.Slot,
                    RelayFunction = r.RelayFunction,
                    OutputKind = (OutputKind)r.OutputKind,
                    PairSlot = r.PairSlot,
                    TravelSeconds = r.TravelSeconds,
                    DeadTimeSeconds = r.DeadTimeSeconds,
                    PwmFrequencyHz = r.PwmFrequencyHz,
                    ServoMinPulseUs = r.ServoMinPulseUs,
                    ServoMaxPulseUs = r.ServoMaxPulseUs,
                    ServoSafePositionPercent = r.ServoSafePositionPercent,
                    LatchingPulseMs = r.LatchingPulseMs,
                    RateLimitPercentPerSecond = r.RateLimitPercentPerSecond,
                    MinOnSeconds = r.MinOnSeconds,
                    MinOffSeconds = r.MinOffSeconds,
                    TimeProportioningPeriodSeconds = r.TimeProportioningPeriodSeconds,
                })
                .ToListAsync();
            IList<DeviceFunctionControl> functionControls = await db.DeviceConfigControllerFunctionControls.AsNoTracking()
                .Where(f => f.IDDeviceConfigController == deviceConfigControllerID)
                .Select(f => new DeviceFunctionControl
                {
                    RelayFunction = (RelayFunction)f.RelayFunction,
                    ControlMode = (ControlMode)f.ControlMode,
                    PidSetpointMetric = f.PidSetpointMetric == null ? null : (SensorMetric)f.PidSetpointMetric,
                    PidSetpoint = f.PidSetpoint,
                    PidKp = f.PidKp,
                    PidKi = f.PidKi,
                    PidKd = f.PidKd,
                    PidSampleIntervalSeconds = f.PidSampleIntervalSeconds,
                })
                .ToListAsync();
            return ToDto(row, relays, functionControls);
        }

        public async Task<Device?> DeviceGetByDeviceConfigSensorIdAsync(int? deviceConfigSensorID)
        {
            var row = await db.Devices.AsNoTracking()
                .FirstOrDefaultAsync(d => d.DeviceConfigSensorID == deviceConfigSensorID);
            return row == null ? null : ToDto(row);
        }

        public async Task<Device?> DeviceGetByDeviceConfigControllerIdAsync(int? deviceConfigControllerID)
        {
            var row = await db.Devices.AsNoTracking()
                .FirstOrDefaultAsync(d => d.DeviceConfigControllerID == deviceConfigControllerID);
            return row == null ? null : ToDto(row);
        }

        /// Returns null on success, or a validation error message (caller returns 400) without writing anything.
        public async Task<string?> DeviceConfigControllerUpdateAsync(int? idDevice, DeviceConfigController? cfg)
        {
            if (cfg == null)
            {
                return null;
            }

            var assignedSlots = cfg.Relays.Where(s => s.RelayFunction != 0).ToList();
            if (assignedSlots.Any(s => s.Slot < 1 || s.Slot > RelaySlotLimits.MaxSlots))
            {
                return $"Relay slot must be between 1 and {RelaySlotLimits.MaxSlots}.";
            }
            if (assignedSlots.Select(s => s.Slot).Distinct().Count() != assignedSlots.Count)
            {
                return "Duplicate relay slot in request.";
            }
            // RelayPair/LatchingPulse drive TWO physical slots (open+close) - PairSlot must point at a real, different, currently-unassigned-to-anything-else slot number.
            foreach (DeviceRelaySlot slot in assignedSlots.Where(s => s.OutputKind is OutputKind.RelayPair or OutputKind.LatchingPulse))
            {
                if (slot.PairSlot is not int pairSlot || pairSlot < 1 || pairSlot > RelaySlotLimits.MaxSlots || pairSlot == slot.Slot)
                {
                    return $"Relay slot {slot.Slot} ({slot.OutputKind}): pairSlot must be a different valid slot number.";
                }
            }

            var pidFunctions = cfg.FunctionControl.Where(f => f.ControlMode == ControlMode.Pid).ToList();
            if (cfg.FunctionControl.Select(f => f.RelayFunction).Distinct().Count() != cfg.FunctionControl.Count)
            {
                return "Duplicate relay function in functionControls request.";
            }
            foreach (DeviceFunctionControl fc in pidFunctions)
            {
                if (fc.PidSetpointMetric is not SensorMetric metric || !PidSetpointMetricLimits.Allowed.Contains(metric))
                {
                    return $"{fc.RelayFunction}: Pid mode requires pidSetpointMetric to be one of {string.Join(", ", PidSetpointMetricLimits.Allowed)}.";
                }
                if (fc.PidSampleIntervalSeconds is not double interval || interval <= 0)
                {
                    return $"{fc.RelayFunction}: Pid mode requires a positive pidSampleIntervalSeconds.";
                }
            }

            // Resolve from idDevice's OWN DeviceConfigControllerID, not cfg.IDDeviceConfigController - a client-supplied id could otherwise overwrite another device's controller config.
            int? ownConfigControllerId = await db.Devices.AsNoTracking()
                .Where(d => d.IDDevice == idDevice)
                .Select(d => d.DeviceConfigControllerID)
                .FirstOrDefaultAsync();

            // Delete-then-insert in one transaction - a crash between the two would otherwise leave the device with zero relay slots.
            await using var transaction = await db.Database.BeginTransactionAsync();

            var row = await db.DeviceConfigControllers
                .FirstOrDefaultAsync(c => c.IDDeviceConfigController == ownConfigControllerId);
            if (row != null)
            {
                // Only the relay-pin mapping lives here now - threshold/hysteresis/interval/schedule config moved to the device's assigned DeviceFarmUnitZone, edited from the Zone page instead.
                row.RelayEnabled = cfg.RelayEnabled;

                // Wholesale replace: delete every existing slot row for this controller, then insert one row per ASSIGNED slot (RelayFunction 0/Disabled is omitted, not stored) - simpler and less error-prone than diffing against the posted set.
                await db.DeviceConfigControllerRelays
                    .Where(r => r.IDDeviceConfigController == ownConfigControllerId)
                    .ExecuteDeleteAsync();
                foreach (DeviceRelaySlot slot in assignedSlots)
                {
                    db.DeviceConfigControllerRelays.Add(new DeviceConfigControllerRelayRow
                    {
                        IDDeviceConfigController = ownConfigControllerId!.Value,
                        Slot = slot.Slot,
                        RelayFunction = slot.RelayFunction,
                        OutputKind = (int)slot.OutputKind,
                        PairSlot = slot.PairSlot,
                        TravelSeconds = slot.TravelSeconds,
                        DeadTimeSeconds = slot.DeadTimeSeconds,
                        PwmFrequencyHz = slot.PwmFrequencyHz,
                        ServoMinPulseUs = slot.ServoMinPulseUs,
                        ServoMaxPulseUs = slot.ServoMaxPulseUs,
                        ServoSafePositionPercent = slot.ServoSafePositionPercent,
                        LatchingPulseMs = slot.LatchingPulseMs,
                        RateLimitPercentPerSecond = slot.RateLimitPercentPerSecond,
                        MinOnSeconds = slot.MinOnSeconds,
                        MinOffSeconds = slot.MinOffSeconds,
                        TimeProportioningPeriodSeconds = slot.TimeProportioningPeriodSeconds,
                    });
                }

                // Same wholesale-replace pattern as Relays above - only non-Threshold (Pid) functions are stored, a function absent from the posted set reverts to Threshold.
                await db.DeviceConfigControllerFunctionControls
                    .Where(f => f.IDDeviceConfigController == ownConfigControllerId)
                    .ExecuteDeleteAsync();
                foreach (DeviceFunctionControl fc in pidFunctions)
                {
                    db.DeviceConfigControllerFunctionControls.Add(new DeviceConfigControllerFunctionControlRow
                    {
                        IDDeviceConfigController = ownConfigControllerId!.Value,
                        RelayFunction = (int)fc.RelayFunction,
                        ControlMode = (int)fc.ControlMode,
                        PidSetpointMetric = (int?)fc.PidSetpointMetric,
                        PidSetpoint = fc.PidSetpoint,
                        PidKp = fc.PidKp,
                        PidKi = fc.PidKi,
                        PidKd = fc.PidKd,
                        PidSampleIntervalSeconds = fc.PidSampleIntervalSeconds,
                    });
                }
            }

            var deviceRow = await db.Devices.FirstOrDefaultAsync(d => d.IDDevice == idDevice);
            if (deviceRow != null)
            {
                deviceRow.ConfigVersion = (deviceRow.ConfigVersion ?? 0) + 1;
            }

            await db.SaveChangesAsync();
            await transaction.CommitAsync();

            // Outside the transaction on purpose - AddOutboxItemAsync's dedup relies on catching a unique-constraint violation, which on Postgres would poison this same transaction for what's a routine, expected outcome, not a rare edge case.
            if (deviceRow != null)
            {
                await outboxRepository.AddOutboxItemAsync(deviceRow.IDDevice, CommandActionType.ConfigChanged, DateTime.UtcNow, DateTime.UtcNow.AddDays(30));
            }
            return null;
        }

        public async Task DeviceConfigSensorUpdateAsync(int? idDevice, DeviceConfigSensor? cfg)
        {
            if (cfg == null)
            {
                return;
            }

            // Same ownership-lookup rule as DeviceConfigControllerUpdateAsync above.
            int? ownConfigSensorId = await db.Devices.AsNoTracking()
                .Where(d => d.IDDevice == idDevice)
                .Select(d => d.DeviceConfigSensorID)
                .FirstOrDefaultAsync();

            var row = await db.DeviceConfigSensors
                .FirstOrDefaultAsync(c => c.IDDeviceConfigSensor == ownConfigSensorId);
            if (row != null)
            {
                row.SensorBattery = cfg.SensorBattery;
                row.BatteryDividerR1 = cfg.BatteryDividerR1;
                row.BatteryDividerR2 = cfg.BatteryDividerR2;
                row.SensorTemp = cfg.SensorTemp;
                row.SensorTempSoil = cfg.SensorTempSoil;
                row.SensorHumid = cfg.SensorHumid;
                row.SensorMoist = cfg.SensorMoist;
                row.SensorLight = cfg.SensorLight;
                row.SensorCo2 = cfg.SensorCo2;
                row.SensorTvoc = cfg.SensorTvoc;
                row.SensorBarometer = cfg.SensorBarometer;
                row.SensorPH = cfg.SensorPH;
                row.SensorRainLevel = cfg.SensorRainLevel;
                row.SensorWaterLevel = cfg.SensorWaterLevel;
                row.SensorWind = cfg.SensorWind;
                row.SensorEc = cfg.SensorEc;
                row.SensorWeight = cfg.SensorWeight;
                row.WeightCalibrationFactor = cfg.WeightCalibrationFactor;
                row.WeightTareOffset = cfg.WeightTareOffset;
                row.EcCalibrationSlope = cfg.EcCalibrationSlope;
                row.EcCalibrationOffset = cfg.EcCalibrationOffset;
            }

            var deviceRow = await db.Devices.FirstOrDefaultAsync(d => d.IDDevice == idDevice);
            if (deviceRow != null)
            {
                deviceRow.ConfigVersion = (deviceRow.ConfigVersion ?? 0) + 1;
            }

            await db.SaveChangesAsync();
            if (deviceRow != null)
            {
                await outboxRepository.AddOutboxItemAsync(deviceRow.IDDevice, CommandActionType.ConfigChanged, DateTime.UtcNow, DateTime.UtcNow.AddDays(30));
            }
        }

        private static DeviceConfigSensor ToDto(DeviceConfigSensorRow c) => new()
        {
            IDDeviceConfigSensor = c.IDDeviceConfigSensor,
            SensorBattery = c.SensorBattery,
            BatteryDividerR1 = c.BatteryDividerR1,
            BatteryDividerR2 = c.BatteryDividerR2,
            SensorTemp = c.SensorTemp,
            SensorTempSoil = c.SensorTempSoil,
            SensorHumid = c.SensorHumid,
            SensorMoist = c.SensorMoist,
            SensorLight = c.SensorLight,
            SensorCo2 = c.SensorCo2,
            SensorTvoc = c.SensorTvoc,
            SensorBarometer = c.SensorBarometer,
            SensorPH = c.SensorPH,
            SensorRainLevel = c.SensorRainLevel,
            SensorWaterLevel = c.SensorWaterLevel,
            SensorWind = c.SensorWind,
            SensorEc = c.SensorEc,
            SensorWeight = c.SensorWeight,
            WeightCalibrationFactor = c.WeightCalibrationFactor,
            WeightTareOffset = c.WeightTareOffset,
            EcCalibrationSlope = c.EcCalibrationSlope,
            EcCalibrationOffset = c.EcCalibrationOffset,
        };

        // Relay-pin mapping only - Rules/WaterPumpMaxRunSeconds/WaterPumpCooldownSeconds on the DTO come from the assigned zone via DeviceApiController.BuildDeviceConfigAsync, not this row.
        private static DeviceConfigController ToDto(DeviceConfigControllerRow c, IList<DeviceRelaySlot> relays, IList<DeviceFunctionControl> functionControls) => new()
        {
            IDDeviceConfigController = c.IDDeviceConfigController,
            RelayEnabled = c.RelayEnabled,
            Relays = relays,
            FunctionControl = functionControls,
        };
    }
}
