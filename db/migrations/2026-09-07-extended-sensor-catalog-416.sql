-- Roadmap #416: extended sensor catalog (28 new sensor models, see AgrumyFirmware's parallel
-- migration). Two genuinely new physical quantities (EC, load-cell weight) get their own
-- sensorData column + deviceConfigSensor slot; every other new sensor reuses an EXISTING
-- config slot (SensorTemp/Humid/Barometer/Co2/Moist/Light/PH) as an additional selectable
-- deviceTypeSensor row - same convention as BH1750/BME280/CCS811 already do.
--
-- WHY THIS IS MANUAL: see 2026-08-31-deviceDiagnostic-table.sql.
-- SAFE TO RE-RUN: ADD COLUMN/CONSTRAINT IF NOT EXISTS; the deviceTypeSensor INSERTs use
-- INSERT IGNORE (primary key collision is a no-op, not an error).

ALTER TABLE `sensorData`
    ADD COLUMN IF NOT EXISTS `Ec` double NULL,
    ADD COLUMN IF NOT EXISTS `Weight` double NULL;

ALTER TABLE `deviceConfigSensor`
    ADD COLUMN IF NOT EXISTS `SensorEc` int(11) NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS `SensorWeight` int(11) NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS `WeightCalibrationFactor` double NULL,
    ADD COLUMN IF NOT EXISTS `EcCalibrationSlope` double NULL,
    ADD COLUMN IF NOT EXISTS `EcCalibrationOffset` double NULL;

-- MariaDB has no ADD CONSTRAINT IF NOT EXISTS for FOREIGN KEY (unlike ADD COLUMN/ADD INDEX above) -
-- re-running this migration a second time will error here with a duplicate constraint name; that's
-- expected and means everything below already applied, not a real failure.
ALTER TABLE `deviceConfigSensor`
    ADD CONSTRAINT `FK_deviceConfigSensor_deviceTypeSensor_SensorEc` FOREIGN KEY (`SensorEc`) REFERENCES `deviceTypeSensor` (`IDDeviceTypeSensor`),
    ADD CONSTRAINT `FK_deviceConfigSensor_deviceTypeSensor_SensorWeight` FOREIGN KEY (`SensorWeight`) REFERENCES `deviceTypeSensor` (`IDDeviceTypeSensor`);

ALTER TABLE `deviceTypeSensor`
    ADD COLUMN IF NOT EXISTS `Ec` int(11) NULL,
    ADD COLUMN IF NOT EXISTS `Weight` int(11) NULL;

UPDATE `deviceTypeSensor` SET `Ec` = 1, `Weight` = 1 WHERE `IDDeviceTypeSensor` = 0;

INSERT IGNORE INTO `deviceTypeSensor` (`IDDeviceTypeSensor`, `SensorName`, `SensorDescription`, `Temperature`, `TemperatureSoil`, `Humidity`, `Barometer`, `Co2`, `Moisture`, `WaterPH`, `Light`, `Ec`, `Weight`) VALUES
    (1010, 'MAX31855', 'K-type thermocouple amplifier, SPI', 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
    (1011, 'MAX31856', 'Thermocouple amplifier, SPI', 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
    (1012, 'MAX31865', 'PT100 RTD amplifier, SPI', 1, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
    (1013, 'MLX90614', 'Non-contact IR thermometer, I2C', 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
    (1014, 'MCP9808', NULL, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
    (1015, 'AHT20/AHT21', NULL, 1, NULL, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
    (1016, 'AM2320', NULL, 1, NULL, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
    (1017, 'HTU21DF', NULL, 1, NULL, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
    (1018, 'Si7021', NULL, 1, NULL, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
    (1019, 'SHT31', NULL, 1, NULL, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
    (1020, 'SHT4x', NULL, 1, NULL, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
    (1021, 'SHTC3', NULL, 1, NULL, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
    (1022, 'BME680', NULL, 1, NULL, 1, 1, NULL, NULL, NULL, NULL, NULL, NULL),
    (1023, 'DPS310', NULL, 1, NULL, NULL, 1, NULL, NULL, NULL, NULL, NULL, NULL),
    (1024, 'SCD30', 'NDIR CO2, I2C', NULL, NULL, NULL, NULL, 1, NULL, NULL, NULL, NULL, NULL),
    (1025, 'SCD4x', 'Photoacoustic CO2, I2C', NULL, NULL, NULL, NULL, 1, NULL, NULL, NULL, NULL, NULL),
    (1026, 'MH-Z19', 'NDIR CO2, UART (Serial2, fixed pins)', NULL, NULL, NULL, NULL, 1, NULL, NULL, NULL, NULL, NULL),
    (1027, 'Chirp soil moisture', 'Capacitive I2C soil moisture', NULL, NULL, NULL, NULL, NULL, 1, NULL, NULL, NULL, NULL),
    (1028, 'Atlas Scientific EZO pH', 'I2C, address 0x63', NULL, NULL, NULL, NULL, NULL, NULL, 1, NULL, NULL, NULL),
    (1029, 'Anyleaf pH', 'Analog pH probe, ConfigPin.PH', NULL, NULL, NULL, NULL, NULL, NULL, 1, NULL, NULL, NULL),
    (1030, 'ADS1115 EC probe', 'Analog EC probe via ADS1115 ADC - needs per-install ecCalibrationSlope/Offset, no universal formula', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 1, NULL),
    (1031, 'TSL2561', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 1, NULL, NULL),
    (1032, 'TSL2591', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 1, NULL, NULL),
    (1033, 'SI1145', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 1, NULL, NULL),
    (1034, 'LTR390', 'Ambient light channel only, UV channel unused', NULL, NULL, NULL, NULL, NULL, NULL, NULL, 1, NULL, NULL),
    (1035, 'VEML7700', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 1, NULL, NULL),
    (1036, 'AS7341', 'Reports raw spectral clear-channel count, not calibrated lux', NULL, NULL, NULL, NULL, NULL, NULL, NULL, 1, NULL, NULL),
    (1037, 'HX711 load cell', 'Needs weightCalibrationFactor set per install (set_scale())', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 1);

-- Sanity check after running:
--   SHOW COLUMNS FROM sensorData LIKE 'Ec'; SHOW COLUMNS FROM sensorData LIKE 'Weight';
--   SHOW COLUMNS FROM deviceConfigSensor LIKE 'Sensor%';
--   SELECT COUNT(*) FROM deviceTypeSensor;  -- expect 41 (13 existing + 28 new)
