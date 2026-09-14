using Agrumy.Shared.Models;

namespace Agrumy.Api.Weather
{
    /// Cascades from the most specific real-world pin available down to the organization/server-wide default - same
    /// "fall back one level at a time" shape as AstronomicalRuleResolver's own location lookup. A greenhouse Unit or
    /// Farm complex, and an Open-Field parcel/zone, can each have their own pin (dropped via point-location-map.js /
    /// parcel-geometry-map.js), so two sites under the same organization no longer have to share one forecast.
    public static class WeatherLocationResolver
    {
        public static (double Lat, double Lon)? ForGreenhouseUnit(DeviceFarmUnit? unit, DeviceFarm? farm, Tenant tenant, ServerConfig config) =>
            Pick(unit?.Latitude, unit?.Longitude)
            ?? Pick(farm?.Latitude, farm?.Longitude)
            ?? ForTenantDefault(tenant, config);

        public static (double Lat, double Lon)? ForParcelZone(FarmParcelZone zone, FarmParcel? parcel, Tenant tenant, ServerConfig config) =>
            BboxCenter(zone.BboxMinLat, zone.BboxMinLon, zone.BboxMaxLat, zone.BboxMaxLon)
            ?? ForParcel(parcel, tenant, config);

        public static (double Lat, double Lon)? ForParcel(FarmParcel? parcel, Tenant tenant, ServerConfig config) =>
            BboxCenter(parcel?.BboxMinLat, parcel?.BboxMinLon, parcel?.BboxMaxLat, parcel?.BboxMaxLon)
            ?? ForTenantDefault(tenant, config);

        /// Used where no more specific node is in scope (the Tenant/WeatherState summary endpoint, a rule with no resolvable Unit/Zone) - the organization's own pin, falling back to the server-wide default.
        public static (double Lat, double Lon)? ForTenantDefault(Tenant tenant, ServerConfig config) =>
            Pick(tenant.Latitude, tenant.Longitude) ?? Pick(config.WeatherLocationLat, config.WeatherLocationLon);

        private static (double, double)? Pick(double? lat, double? lon) => lat is double la && lon is double lo ? (la, lo) : null;

        private static (double, double)? BboxCenter(double? minLat, double? minLon, double? maxLat, double? maxLon) =>
            minLat is double a && minLon is double b && maxLat is double c && maxLon is double d ? ((a + c) / 2, (b + d) / 2) : null;
    }
}
