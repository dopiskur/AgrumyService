namespace api.Models
{
    /// Canonical deviceTypeSensor IDs - must match AgrumyFirmware's SensorController.h/.cpp SensorTypeIds:: constants exactly, renumbering desyncs the two independently-versioned repos.
    public static class SensorTypeIds
    {
        public const int Disabled = 0;
        public const int Dht11 = 1001;
        public const int Dht22 = 1002;
        public const int Bmp180 = 1003;
        public const int Bmp280 = 1004;
        public const int Bme280 = 1005;
        public const int Ccs811 = 1006;
        public const int Ds18B20 = 1007;
        public const int Bh1750 = 1008;
        public const int Max17048 = 1009;
        // Roadmap #416 - extended catalog, one ID per physical sensor model.
        public const int Max31855 = 1010;
        public const int Max31856 = 1011;
        public const int Max31865 = 1012;
        public const int Mlx90614 = 1013;
        public const int Mcp9808 = 1014;
        public const int Aht = 1015;
        public const int Am2320 = 1016;
        public const int Htu21Df = 1017;
        public const int Si7021 = 1018;
        public const int Sht31 = 1019;
        public const int Sht4x = 1020;
        public const int Shtc3 = 1021;
        public const int Bme680 = 1022;
        public const int Dps310 = 1023;
        public const int Scd30 = 1024;
        public const int Scd4x = 1025;
        public const int Mhz19 = 1026;
        public const int ChirpSoilMoisture = 1027;
        public const int EzoPh = 1028;
        public const int AnyleafPh = 1029;
        public const int Ads1115Ec = 1030;
        public const int Tsl2561 = 1031;
        public const int Tsl2591 = 1032;
        public const int Si1145 = 1033;
        public const int Ltr390 = 1034;
        public const int Veml7700 = 1035;
        public const int As7341 = 1036;
        public const int Hx711 = 1037;
        public const int AnalogVoltage = 2001;
        public const int AnalogMoisture = 2002;
        public const int AnalogWaterLevel = 2003;
    }
}
