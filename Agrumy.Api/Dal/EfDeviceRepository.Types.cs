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
        // ---- Fixed device-type lookup lists -----------------------------

        public async Task<IList<DeviceRole>> DeviceRoleGetAsync()
        {
            return await db.DeviceRoles.AsNoTracking()
                .Select(t => new DeviceRole
                {
                    IDDeviceRole = t.IDDeviceRole,
                    DeviceRoleName = t.DeviceRoleName,
                    SensorEnabled = t.SensorEnabled,
                    ControllerEnabled = t.ControllerEnabled,
                })
                .ToListAsync();
        }

        public async Task<IList<DeviceType>> DeviceTypeGetAsync()
        {
            return await db.DeviceTypes.AsNoTracking()
                .Select(k => new DeviceType { IDDeviceType = k.IDDeviceType, Kit = k.Kit, ControllerCapable = k.ControllerCapable, PinoutJson = k.PinoutJson })
                .ToListAsync();
        }

        public async Task<IList<DeviceTypeService>> DeviceTypeServiceGetAsync()
        {
            return await db.DeviceTypeServices.AsNoTracking()
                .Select(s => new DeviceTypeService { IDDeviceTypeService = s.IDDeviceTypeService, ServiceType = s.ServiceType })
                .ToListAsync();
        }

        public async Task<IList<DeviceTypeRelay>> DeviceTypeRelayGetAsync()
        {
            return await db.DeviceTypeRelays.AsNoTracking()
                .Select(r => new DeviceTypeRelay { IDDeviceTypeRelay = r.IDDeviceTypeRelay, RelayName = r.RelayName })
                .ToListAsync();
        }

        public async Task<IList<DeviceTypeSensor>> DeviceTypeSensorGetAsync()
        {
            return await db.DeviceTypeSensors.AsNoTracking()
                .Select(s => new DeviceTypeSensor
                {
                    IDDeviceTypeSensor = s.IDDeviceTypeSensor,
                    SensorName = s.SensorName,
                    SensorDescription = s.SensorDescription,
                    Battery = s.Battery,
                    Temperature = s.Temperature,
                    TemperatureSoil = s.TemperatureSoil,
                    Humidity = s.Humidity,
                    Moisture = s.Moisture,
                    Light = s.Light,
                    Co2 = s.Co2,
                    Tvoc = s.Tvoc,
                    Barometer = s.Barometer,
                    WaterPH = s.WaterPH,
                    WaterTankLevel = s.WaterTankLevel,
                    RainLevel = s.RainLevel,
                    Wind = s.Wind,
                    Ec = s.Ec,
                    Weight = s.Weight,
                })
                .ToListAsync();
        }
    }
}
