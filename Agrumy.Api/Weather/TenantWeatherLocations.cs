using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;

namespace Agrumy.Api.Weather
{
    /// Every distinct real-world pin a tenant's greenhouse Units/Farms and Open-Field parcels/zones resolve to
    /// (WeatherLocationResolver), deduped so WeatherEvaluator/FrostAlertEvaluator hit OpenWeatherMap once per site,
    /// not once per zone. Always includes the tenant's own default location (ForTenantDefault) even when nothing has
    /// its own pin yet, so the Tenant/WeatherState summary endpoint and any rule that falls through to it always have
    /// a resolvable, up-to-date location to read.
    public static class TenantWeatherLocations
    {
        public static async Task<IReadOnlyList<(double Lat, double Lon)>> ResolveDistinctAsync(
            IDeviceFarmUnitRepository unitRepo, IFarmParcelRepository farmParcelRepo, ISimulationRepository simulationRepo, int tenantId, Tenant tenant, ServerConfig config)
        {
            var locations = new HashSet<(double, double)>();
            if (WeatherLocationResolver.ForTenantDefault(tenant, config) is (double, double) def)
            {
                locations.Add(def);
            }

            IList<DeviceFarm> farms = await unitRepo.DeviceFarmsGetAsync(tenantId);
            Dictionary<int, DeviceFarm> farmsById = farms.Where(f => f.IDDeviceFarm is int).ToDictionary(f => f.IDDeviceFarm!.Value);
            foreach (DeviceFarmUnit unit in await unitRepo.DeviceFarmUnitsGetAsync(tenantId))
            {
                DeviceFarm? farm = unit.DeviceFarmID is int farmId && farmsById.TryGetValue(farmId, out DeviceFarm? f) ? f : null;
                if (WeatherLocationResolver.ForGreenhouseUnit(unit, farm, tenant, config) is (double, double) unitLoc)
                {
                    locations.Add(unitLoc);
                }
            }

            // Every parcel's own bbox-center is warmed regardless of per-zone geometry state, so a zone with no boundary of its own (falling back to the parcel's) still resolves to an already-cached location below.
            var parcelsById = new Dictionary<int, FarmParcel>();
            foreach (DeviceFarm farm in farms.Where(f => f.FarmType == FarmType.OpenField && f.IDDeviceFarm is int))
            {
                foreach (FarmParcel parcel in await farmParcelRepo.FarmParcelsGetAsync(farm.IDDeviceFarm!.Value))
                {
                    if (parcel.IDFarmParcel is int parcelId)
                    {
                        parcelsById[parcelId] = parcel;
                    }
                    if (WeatherLocationResolver.ForParcel(parcel, tenant, config) is (double, double) parcelLoc)
                    {
                        locations.Add(parcelLoc);
                    }
                }
            }
            foreach (FarmParcelZone zone in await farmParcelRepo.FarmParcelZonesWithGeometryGetAsync(tenantId))
            {
                FarmParcel? parcel = parcelsById.TryGetValue(zone.FarmParcelID, out FarmParcel? p) ? p : null;
                if (WeatherLocationResolver.ForParcelZone(zone, parcel, tenant, config) is (double, double) zoneLoc)
                {
                    locations.Add(zoneLoc);
                }
            }

            foreach ((double Lat, double Lon) feedLoc in await simulationRepo.ActiveWeatherFeedDeviceLocationsGetAsync(tenantId))
            {
                locations.Add(feedLoc);
            }

            return locations.ToList();
        }
    }
}
