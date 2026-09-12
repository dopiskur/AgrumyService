using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Api.Security;
using Agrumy.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Agrumy.Api.Controllers.API
{
    [Route("/api/SensorData")]
    public class SensorDataController(ISensorDataRepository sensorDataRepo, IDeviceRepository deviceRepo, IUserRepository userRepo, IAuditLogRepository auditLogRepo, ICache cache, Agrumy.Api.Quota.TenantQuotaEnforcer quotaEnforcer) : ApiControllerBase(userRepo, auditLogRepo, cache)
    {
        // Bounds how large a window a bucketed query can ask the DB to aggregate - EfSensorDataRepository enforces the same cap as a backstop (returns "" rather than throwing), this is just so the caller gets a real 400 instead of a silently empty result.
        private const int MaxWindowDays = 400;
        private static bool IsValidWindow(DateTimeOffset from, DateTimeOffset to) => to > from && (to - from) <= TimeSpan.FromDays(MaxWindowDays);

        [HttpGet]
        [Authorize]
        public async Task<ActionResult<string>> Get(int? deviceID, DateTimeOffset from, DateTimeOffset to, SensorDataBucket bucket = SensorDataBucket.Hour)
        {
            if (!IsValidWindow(from, to))
            {
                return BadRequest($"Invalid window: 'to' must be after 'from', and the span must not exceed {MaxWindowDays} days.");
            }

            // Stays organization-scoped even for a Global reader, unlike DevicesGet/DeviceFleetGet.
            return Ok(await sensorDataRepo.SensorDataGetAsync(CallerReadsDevicesGlobally ? null : CallerTenantId, deviceID, from, to, bucket));
        }

        // A real device batch is ~20-30 readings (RAM spills at 8192 bytes) - generous headroom.
        private const int MaxSensorDataBatchSize = 1000;

        [HttpPost]
        [EnableRateLimiting("device-data")]
        [Authorize(Policy = DeviceAuth.SessionPolicy)]
        public async Task<ActionResult<int?>> Post([FromBody] List<SensorDataPushReading> readings)
        {
            if (readings.Count > MaxSensorDataBatchSize)
            {
                return BadRequest($"Batch too large: {readings.Count} readings, max {MaxSensorDataBatchSize} per request.");
            }

            string apiId = HttpContext.DeviceApiId()!;

            // Device/organization come from the authenticated identity, never from the payload.
            Device? device = await deviceRepo.DeviceGetByApiIdAsync(apiId);
            if (device is null)
            {
                return Unauthorized();
            }

            // Catches a device (compromised, buggy, or just ignoring its own configured sleepSeconds) pushing telemetry faster than its organization's quota allows - separate from DeviceUpdate's CheckMinSensorIntervalAsync, which only gates the CONFIGURED value.
            if (await quotaEnforcer.CheckSensorPushIntervalAsync(device.TenantID, device.IDDevice!.Value) is string intervalLimitError)
            {
                return ForbidWith(intervalLimitError);
            }

            await sensorDataRepo.SensorDataPushAsync(readings, device.IDDevice!.Value, device.TenantID ?? 0,
                device.DeviceFarmUnitID, device.DeviceFarmUnitZoneID);

            return Ok(device.ConfigVersion);
        }

        /// Deleting telemetry is device management, gated to the device-manager roles; the target device's own organization is resolved and checked explicitly.
        [HttpDelete]
        [Authorize(Roles = RoleNames.DeviceManagers)]
        public async Task<ActionResult> Delete(int deviceID, DateTimeOffset olderThan)
        {
            Device? device = await deviceRepo.DeviceGetByIdAsync(deviceID);
            if (device is null)
            {
                return NotFound();
            }
            if (!CallerManagesDevices(device.TenantID))
            {
                return ForbidWith("Device belongs to a different tenant");
            }

            await sensorDataRepo.SensorDataDeleteAsync(device.TenantID, deviceID, olderThan);
            return Ok();
        }

        [HttpGet("ZoneAverage")]
        [Authorize]
        public async Task<ActionResult<string>> ZoneAverageGet(int deviceFarmUnitZoneID, DateTimeOffset from, DateTimeOffset to, SensorDataBucket bucket = SensorDataBucket.Hour)
        {
            if (!IsValidWindow(from, to))
            {
                return BadRequest($"Invalid window: 'to' must be after 'from', and the span must not exceed {MaxWindowDays} days.");
            }
            return Ok(await sensorDataRepo.SensorDataZoneAverageGetAsync(CallerReadsDevicesGlobally ? null : CallerTenantId, deviceFarmUnitZoneID, from, to, bucket));
        }

        [HttpGet("UnitAverage")]
        [Authorize]
        public async Task<ActionResult<string>> UnitAverageGet(int deviceFarmUnitID, DateTimeOffset from, DateTimeOffset to, SensorDataBucket bucket = SensorDataBucket.Hour)
        {
            if (!IsValidWindow(from, to))
            {
                return BadRequest($"Invalid window: 'to' must be after 'from', and the span must not exceed {MaxWindowDays} days.");
            }
            return Ok(await sensorDataRepo.SensorDataUnitAverageGetAsync(CallerReadsDevicesGlobally ? null : CallerTenantId, deviceFarmUnitID, from, to, bucket));
        }
    }
}
