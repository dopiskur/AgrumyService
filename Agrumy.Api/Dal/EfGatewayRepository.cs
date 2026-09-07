using Agrumy.Dal;
using Agrumy.Dal.Entities;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace Agrumy.Api.Dal
{
    /// IGatewayRepository, extracted out of the EfRepository god class (roadmap #246) - reads db.Devices directly rather than calling into IDeviceRepository, and reuses EfDeviceRepository.ToDto for the one DeviceRow-to-Device mapping it needs.
    internal sealed class EfGatewayRepository(AgrumyDbContext db) : IGatewayRepository
    {
        public async Task<IList<Device>> GatewayDevicesGetAllAsync()
        {
            // LoRaGatewayEnabled (roadmap #383) lists alongside the classic IsGateway (standalone Agrumy.Gateway) devices - both relay through the same GatewayApiController.Batch path.
            var rows = await db.Devices.AsNoTracking().Where(d => d.IsGateway || d.LoRaGatewayEnabled == true).ToListAsync();
            return rows.Select(EfDeviceRepository.ToDto).ToList();
        }

        public async Task<IList<GatewayDeviceMapping>> GatewayDeviceMappingsGetAsync(int idGatewayDevice) =>
            await MappingsQuery(idGatewayDevice, includeSecrets: false).ToListAsync();

        public async Task<IList<GatewayDeviceMapping>> GatewayDeviceMappingsWithSecretsGetAsync(int idGatewayDevice)
        {
            List<GatewayDeviceMapping> rows = await MappingsQuery(idGatewayDevice, includeSecrets: true).ToListAsync();
            // Swap the raw ApiKey the query above just fetched for a short-lived scoped token before it leaves this method - a compromised gateway then leaks only a time-boxed proof, not every mapped device's permanent credential.
            foreach (GatewayDeviceMapping row in rows)
            {
                if (row.DeviceApiId is string apiId && row.DeviceApiKey is string apiKey)
                {
                    row.DeviceApiKey = GatewayDeviceToken.Issue(apiId, apiKey);
                }
            }
            return rows;
        }

        private IQueryable<GatewayDeviceMapping> MappingsQuery(int idGatewayDevice, bool includeSecrets) =>
            from m in db.GatewayDeviceMappings.AsNoTracking()
            join dev in db.Devices.AsNoTracking() on m.IDDevice equals dev.IDDevice
            where m.IDGatewayDevice == idGatewayDevice
            select new GatewayDeviceMapping
            {
                IDGatewayDeviceMapping = m.IDGatewayDeviceMapping,
                IDGatewayDevice = m.IDGatewayDevice,
                DevEUI = m.DevEUI,
                IDDevice = m.IDDevice,
                DeviceName = dev.DeviceName,
                DeviceApiId = dev.ApiId,
                DeviceApiKey = includeSecrets ? dev.ApiKey : null,
                DateCreated = m.DateCreated,
            };

        public async Task<bool> GatewayDeviceMappingAddAsync(int idGatewayDevice, string devEUI, int idDevice, int? gatewayTenantId)
        {
            // Unconditional, no caller-role exception (same reasoning as DeviceFarmUnitApiController's Zone/Assign check) - a gateway must never be handed another tenant's device ApiKey, not even by a Global admin's mistake.
            if (!await db.Devices.AsNoTracking().AnyAsync(d => d.IDDevice == idDevice && d.TenantID == gatewayTenantId))
            {
                return false;
            }
            if (await db.GatewayDeviceMappings.AsNoTracking()
                .AnyAsync(m => m.IDGatewayDevice == idGatewayDevice && m.DevEUI == devEUI))
            {
                return false;
            }

            db.GatewayDeviceMappings.Add(new GatewayDeviceMappingRow
            {
                IDGatewayDevice = idGatewayDevice,
                DevEUI = devEUI,
                IDDevice = idDevice,
            });
            await db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> GatewayDeviceMappingDeleteAsync(int idGatewayDeviceMapping, int idGatewayDevice)
        {
            int rows = await db.GatewayDeviceMappings
                .Where(m => m.IDGatewayDeviceMapping == idGatewayDeviceMapping && m.IDGatewayDevice == idGatewayDevice)
                .ExecuteDeleteAsync();
            return rows > 0;
        }
    }
}
