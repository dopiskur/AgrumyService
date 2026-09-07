namespace api.Utils
{
    /// Dew point (°C) via the Magnus formula from Temperature (°C) + Humidity (%RH); null if either is missing.
    public static class DewPointCalculator
    {
        // Magnus formula constants (Alduchov-Eskridge), same accuracy class as the Tetens VPD formula VpdCalculator already uses.
        private const double A = 17.62;
        private const double B = 243.12;

        public static double? Compute(double? temperatureC, double? humidityPercent)
        {
            if (temperatureC is not double t || humidityPercent is not double rh || rh <= 0)
            {
                return null;
            }
            double gamma = A * t / (B + t) + Math.Log(rh / 100.0);
            return B * gamma / (A - gamma);
        }
    }
}
