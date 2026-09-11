namespace Agrumy.Api.Satellite
{
    /// Sen2Cor Scene Classification Layer pixel validity (Detaljni dizajn S, D5) - shared by every evalscript's dataMask output AND by C# code that needs to recompute ValidPixelPercent from a raw SCL band without re-calling the API.
    public static class SclCloudMask
    {
        /// SCL class codes excluded from "valid": NO_DATA(0), SATURATED_DEFECTIVE(1), CLOUD_SHADOWS(3), UNCLASSIFIED(7), CLOUD_MEDIUM_PROBABILITY(8), CLOUD_HIGH_PROBABILITY(9), THIN_CIRRUS(10). Kept valid: DARK_AREA_PIXELS(2), VEGETATION(4), NOT_VEGETATED(5), WATER(6), SNOW_ICE(11) - NDSI specifically needs snow/ice pixels to mean anything.
        private static readonly HashSet<int> InvalidClasses = [0, 1, 3, 7, 8, 9, 10];

        public static bool IsValid(int sclClass) => !InvalidClasses.Contains(sclClass);

        /// Percentage of pixels IsValid returns true for - the same figure FarmParcelZoneSatelliteScene.ValidPixelPercent stores and Reliable is thresholded against (D5, MinValidPixelPercent).
        public static double ValidPixelPercent(IReadOnlyList<int> sclPixels)
        {
            if (sclPixels.Count == 0)
            {
                return 0;
            }
            int valid = sclPixels.Count(IsValid);
            return 100.0 * valid / sclPixels.Count;
        }
    }
}
