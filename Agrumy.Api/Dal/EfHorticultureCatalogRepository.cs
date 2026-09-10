using Agrumy.Dal;
using Agrumy.Dal.Entities;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Agrumy.Api.Dal
{
    /// IHorticultureCatalogRepository - one near-identical code path per table, switched on HorticultureCatalogType rather than three near-duplicate repository classes.
    internal sealed class EfHorticultureCatalogRepository(AgrumyDbContext db) : IHorticultureCatalogRepository
    {
        public async Task<IList<HorticultureCatalogEntry>> CatalogGetAsync(HorticultureCatalogType type)
        {
            switch (type)
            {
                case HorticultureCatalogType.Crop:
                    return (await db.HorticultureCatalogCrops.AsNoTracking().OrderBy(r => r.Name).ToListAsync()).Select(ToDto).ToList();
                case HorticultureCatalogType.Perma:
                    return (await db.HorticultureCatalogPermas.AsNoTracking().OrderBy(r => r.Name).ToListAsync()).Select(ToDto).ToList();
                case HorticultureCatalogType.Hydroponic:
                    return (await db.HorticultureCatalogHydroponics.AsNoTracking().OrderBy(r => r.Name).ToListAsync()).Select(ToDto).ToList();
                case HorticultureCatalogType.Fruit:
                    return (await db.HorticultureCatalogFruits.AsNoTracking().OrderBy(r => r.Name).ToListAsync()).Select(ToDto).ToList();
                default:
                    return [];
            }
        }

        public async Task<HorticultureCatalogEntry?> CatalogGetByIdAsync(HorticultureCatalogType type, int id)
        {
            switch (type)
            {
                case HorticultureCatalogType.Crop:
                    return ToDtoOrNull(await db.HorticultureCatalogCrops.AsNoTracking().FirstOrDefaultAsync(r => r.ID == id));
                case HorticultureCatalogType.Perma:
                    return ToDtoOrNull(await db.HorticultureCatalogPermas.AsNoTracking().FirstOrDefaultAsync(r => r.ID == id));
                case HorticultureCatalogType.Hydroponic:
                    return ToDtoOrNull(await db.HorticultureCatalogHydroponics.AsNoTracking().FirstOrDefaultAsync(r => r.ID == id));
                case HorticultureCatalogType.Fruit:
                    return ToDtoOrNull(await db.HorticultureCatalogFruits.AsNoTracking().FirstOrDefaultAsync(r => r.ID == id));
                default:
                    return null;
            }
        }

        public async Task<HorticultureCatalogEntry> CatalogAddAsync(HorticultureCatalogType type, HorticultureCatalogEntry entry)
        {
            switch (type)
            {
                case HorticultureCatalogType.Crop:
                {
                    var row = new HorticultureCatalogCropRow();
                    CopyToRow(entry, row);
                    db.HorticultureCatalogCrops.Add(row);
                    await db.SaveChangesAsync();
                    return ToDto(row);
                }
                case HorticultureCatalogType.Perma:
                {
                    var row = new HorticultureCatalogPermaRow();
                    CopyToRow(entry, row);
                    db.HorticultureCatalogPermas.Add(row);
                    await db.SaveChangesAsync();
                    return ToDto(row);
                }
                case HorticultureCatalogType.Hydroponic:
                {
                    var row = new HorticultureCatalogHydroponicRow();
                    CopyToRow(entry, row);
                    db.HorticultureCatalogHydroponics.Add(row);
                    await db.SaveChangesAsync();
                    return ToDto(row);
                }
                case HorticultureCatalogType.Fruit:
                {
                    var row = new HorticultureCatalogFruitRow();
                    CopyToRow(entry, row);
                    db.HorticultureCatalogFruits.Add(row);
                    await db.SaveChangesAsync();
                    return ToDto(row);
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        public async Task<bool> CatalogUpdateAsync(HorticultureCatalogType type, HorticultureCatalogEntry entry)
        {
            switch (type)
            {
                case HorticultureCatalogType.Crop:
                {
                    var row = await db.HorticultureCatalogCrops.FirstOrDefaultAsync(r => r.ID == entry.ID);
                    if (row == null) return false;
                    CopyToRow(entry, row);
                    await db.SaveChangesAsync();
                    return true;
                }
                case HorticultureCatalogType.Perma:
                {
                    var row = await db.HorticultureCatalogPermas.FirstOrDefaultAsync(r => r.ID == entry.ID);
                    if (row == null) return false;
                    CopyToRow(entry, row);
                    await db.SaveChangesAsync();
                    return true;
                }
                case HorticultureCatalogType.Hydroponic:
                {
                    var row = await db.HorticultureCatalogHydroponics.FirstOrDefaultAsync(r => r.ID == entry.ID);
                    if (row == null) return false;
                    CopyToRow(entry, row);
                    await db.SaveChangesAsync();
                    return true;
                }
                case HorticultureCatalogType.Fruit:
                {
                    var row = await db.HorticultureCatalogFruits.FirstOrDefaultAsync(r => r.ID == entry.ID);
                    if (row == null) return false;
                    CopyToRow(entry, row);
                    await db.SaveChangesAsync();
                    return true;
                }
                default:
                    return false;
            }
        }

        public async Task<bool> CatalogDeleteAsync(HorticultureCatalogType type, int id)
        {
            int rows = type switch
            {
                HorticultureCatalogType.Crop => await db.HorticultureCatalogCrops.Where(r => r.ID == id).ExecuteDeleteAsync(),
                HorticultureCatalogType.Perma => await db.HorticultureCatalogPermas.Where(r => r.ID == id).ExecuteDeleteAsync(),
                HorticultureCatalogType.Hydroponic => await db.HorticultureCatalogHydroponics.Where(r => r.ID == id).ExecuteDeleteAsync(),
                HorticultureCatalogType.Fruit => await db.HorticultureCatalogFruits.Where(r => r.ID == id).ExecuteDeleteAsync(),
                _ => 0,
            };
            return rows > 0;
        }

        private static void CopyToRow(HorticultureCatalogEntry entry, HorticultureCatalogCropRow row)
        {
            row.Name = entry.Name;
            row.Description = entry.Description;
            row.AirTempMin = entry.AirTempMin; row.AirTempMax = entry.AirTempMax;
            row.SoilTempMin = entry.SoilTempMin; row.SoilTempMax = entry.SoilTempMax;
            row.AirHumidityMin = entry.AirHumidityMin; row.AirHumidityMax = entry.AirHumidityMax;
            row.SoilMoistureMin = entry.SoilMoistureMin; row.SoilMoistureMax = entry.SoilMoistureMax;
            row.LightMin = entry.LightMin; row.LightMax = entry.LightMax;
            row.SoilPHMin = entry.SoilPHMin; row.SoilPHMax = entry.SoilPHMax;
            row.SoilECMin = entry.SoilECMin; row.SoilECMax = entry.SoilECMax;
            row.Co2Min = entry.Co2Min; row.Co2Max = entry.Co2Max;
        }

        private static void CopyToRow(HorticultureCatalogEntry entry, HorticultureCatalogPermaRow row)
        {
            row.Name = entry.Name;
            row.Description = entry.Description;
            row.AirTempMin = entry.AirTempMin; row.AirTempMax = entry.AirTempMax;
            row.SoilTempMin = entry.SoilTempMin; row.SoilTempMax = entry.SoilTempMax;
            row.AirHumidityMin = entry.AirHumidityMin; row.AirHumidityMax = entry.AirHumidityMax;
            row.SoilMoistureMin = entry.SoilMoistureMin; row.SoilMoistureMax = entry.SoilMoistureMax;
            row.LightMin = entry.LightMin; row.LightMax = entry.LightMax;
            row.SoilPHMin = entry.SoilPHMin; row.SoilPHMax = entry.SoilPHMax;
            row.SoilECMin = entry.SoilECMin; row.SoilECMax = entry.SoilECMax;
            row.Co2Min = entry.Co2Min; row.Co2Max = entry.Co2Max;
        }

        private static void CopyToRow(HorticultureCatalogEntry entry, HorticultureCatalogHydroponicRow row)
        {
            row.Name = entry.Name;
            row.Description = entry.Description;
            row.AirTempMin = entry.AirTempMin; row.AirTempMax = entry.AirTempMax;
            row.SoilTempMin = entry.SoilTempMin; row.SoilTempMax = entry.SoilTempMax;
            row.AirHumidityMin = entry.AirHumidityMin; row.AirHumidityMax = entry.AirHumidityMax;
            row.SoilMoistureMin = entry.SoilMoistureMin; row.SoilMoistureMax = entry.SoilMoistureMax;
            row.LightMin = entry.LightMin; row.LightMax = entry.LightMax;
            row.SoilPHMin = entry.SoilPHMin; row.SoilPHMax = entry.SoilPHMax;
            row.SoilECMin = entry.SoilECMin; row.SoilECMax = entry.SoilECMax;
            row.Co2Min = entry.Co2Min; row.Co2Max = entry.Co2Max;
        }

        private static void CopyToRow(HorticultureCatalogEntry entry, HorticultureCatalogFruitRow row)
        {
            row.Name = entry.Name;
            row.Description = entry.Description;
            row.AirTempMin = entry.AirTempMin; row.AirTempMax = entry.AirTempMax;
            row.SoilTempMin = entry.SoilTempMin; row.SoilTempMax = entry.SoilTempMax;
            row.AirHumidityMin = entry.AirHumidityMin; row.AirHumidityMax = entry.AirHumidityMax;
            row.SoilMoistureMin = entry.SoilMoistureMin; row.SoilMoistureMax = entry.SoilMoistureMax;
            row.LightMin = entry.LightMin; row.LightMax = entry.LightMax;
            row.SoilPHMin = entry.SoilPHMin; row.SoilPHMax = entry.SoilPHMax;
            row.SoilECMin = entry.SoilECMin; row.SoilECMax = entry.SoilECMax;
            row.Co2Min = entry.Co2Min; row.Co2Max = entry.Co2Max;
        }

        private static HorticultureCatalogEntry? ToDtoOrNull(HorticultureCatalogCropRow? row) => row == null ? null : ToDto(row);
        private static HorticultureCatalogEntry? ToDtoOrNull(HorticultureCatalogPermaRow? row) => row == null ? null : ToDto(row);
        private static HorticultureCatalogEntry? ToDtoOrNull(HorticultureCatalogHydroponicRow? row) => row == null ? null : ToDto(row);
        private static HorticultureCatalogEntry? ToDtoOrNull(HorticultureCatalogFruitRow? row) => row == null ? null : ToDto(row);

        private static HorticultureCatalogEntry ToDto(HorticultureCatalogCropRow row) => new()
        {
            ID = row.ID, Name = row.Name, Description = row.Description,
            AirTempMin = row.AirTempMin, AirTempMax = row.AirTempMax,
            SoilTempMin = row.SoilTempMin, SoilTempMax = row.SoilTempMax,
            AirHumidityMin = row.AirHumidityMin, AirHumidityMax = row.AirHumidityMax,
            SoilMoistureMin = row.SoilMoistureMin, SoilMoistureMax = row.SoilMoistureMax,
            LightMin = row.LightMin, LightMax = row.LightMax,
            SoilPHMin = row.SoilPHMin, SoilPHMax = row.SoilPHMax,
            SoilECMin = row.SoilECMin, SoilECMax = row.SoilECMax,
            Co2Min = row.Co2Min, Co2Max = row.Co2Max,
        };

        private static HorticultureCatalogEntry ToDto(HorticultureCatalogPermaRow row) => new()
        {
            ID = row.ID, Name = row.Name, Description = row.Description,
            AirTempMin = row.AirTempMin, AirTempMax = row.AirTempMax,
            SoilTempMin = row.SoilTempMin, SoilTempMax = row.SoilTempMax,
            AirHumidityMin = row.AirHumidityMin, AirHumidityMax = row.AirHumidityMax,
            SoilMoistureMin = row.SoilMoistureMin, SoilMoistureMax = row.SoilMoistureMax,
            LightMin = row.LightMin, LightMax = row.LightMax,
            SoilPHMin = row.SoilPHMin, SoilPHMax = row.SoilPHMax,
            SoilECMin = row.SoilECMin, SoilECMax = row.SoilECMax,
            Co2Min = row.Co2Min, Co2Max = row.Co2Max,
        };

        private static HorticultureCatalogEntry ToDto(HorticultureCatalogHydroponicRow row) => new()
        {
            ID = row.ID, Name = row.Name, Description = row.Description,
            AirTempMin = row.AirTempMin, AirTempMax = row.AirTempMax,
            SoilTempMin = row.SoilTempMin, SoilTempMax = row.SoilTempMax,
            AirHumidityMin = row.AirHumidityMin, AirHumidityMax = row.AirHumidityMax,
            SoilMoistureMin = row.SoilMoistureMin, SoilMoistureMax = row.SoilMoistureMax,
            LightMin = row.LightMin, LightMax = row.LightMax,
            SoilPHMin = row.SoilPHMin, SoilPHMax = row.SoilPHMax,
            SoilECMin = row.SoilECMin, SoilECMax = row.SoilECMax,
            Co2Min = row.Co2Min, Co2Max = row.Co2Max,
        };

        private static HorticultureCatalogEntry ToDto(HorticultureCatalogFruitRow row) => new()
        {
            ID = row.ID, Name = row.Name, Description = row.Description,
            AirTempMin = row.AirTempMin, AirTempMax = row.AirTempMax,
            SoilTempMin = row.SoilTempMin, SoilTempMax = row.SoilTempMax,
            AirHumidityMin = row.AirHumidityMin, AirHumidityMax = row.AirHumidityMax,
            SoilMoistureMin = row.SoilMoistureMin, SoilMoistureMax = row.SoilMoistureMax,
            LightMin = row.LightMin, LightMax = row.LightMax,
            SoilPHMin = row.SoilPHMin, SoilPHMax = row.SoilPHMax,
            SoilECMin = row.SoilECMin, SoilECMax = row.SoilECMax,
            Co2Min = row.Co2Min, Co2Max = row.Co2Max,
        };
    }
}
