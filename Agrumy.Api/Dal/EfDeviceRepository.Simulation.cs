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
        // ---- Simulation Mode overrides -------------------------------------

        public async Task<DeviceSimulation?> DeviceSimulationGetAsync(int deviceID)
        {
            var row = await db.DeviceSimulations.AsNoTracking().FirstOrDefaultAsync(s => s.DeviceID == deviceID);
            return row == null ? null : ToDto(row);
        }

        public async Task DeviceSimulationSetAsync(int deviceID, DeviceSimulation value)
        {
            var row = await db.DeviceSimulations.FirstOrDefaultAsync(s => s.DeviceID == deviceID);
            if (row == null)
            {
                row = new DeviceSimulationRow { DeviceID = deviceID };
                db.DeviceSimulations.Add(row);
            }
            row.Enabled = value.Enabled;
            row.Temperature = value.Temperature;
            row.SoilTemperature = value.SoilTemperature;
            row.Humidity = value.Humidity;
            row.Battery = value.Battery;
            row.Moisture = value.Moisture;
            row.Light = value.Light;
            row.Co2 = value.Co2;
            row.Tvoc = value.Tvoc;
            row.Barometer = value.Barometer;
            row.LiquidPH = value.LiquidPH;
            row.RainLevel = value.RainLevel;
            row.WaterLevel = value.WaterLevel;
            row.Wind = value.Wind;
            row.Latitude = value.Latitude;
            row.Longitude = value.Longitude;
            await db.SaveChangesAsync();
        }

        private static DeviceSimulation ToDto(DeviceSimulationRow s) => new()
        {
            Enabled = s.Enabled,
            Temperature = s.Temperature,
            SoilTemperature = s.SoilTemperature,
            Humidity = s.Humidity,
            Battery = s.Battery,
            Moisture = s.Moisture,
            Light = s.Light,
            Co2 = s.Co2,
            Tvoc = s.Tvoc,
            Barometer = s.Barometer,
            LiquidPH = s.LiquidPH,
            RainLevel = s.RainLevel,
            WaterLevel = s.WaterLevel,
            Wind = s.Wind,
            Latitude = s.Latitude,
            Longitude = s.Longitude,
        };
    }
}
