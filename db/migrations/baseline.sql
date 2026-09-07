-- Consolidated baseline schema for the agrumyapi MySQL/MariaDB database.
-- Replaces 87 incremental db/migrations/*.sql files (2026-08-29 through 2026-09-07) with a
-- single CREATE-from-empty snapshot of the actual invent.hr schema on that date. Precondition
-- for roadmap #247 (formal EF Core migrations): `dotnet ef migrations add InitialBeta` should
-- be generated against this baseline, not replayed against the old 87-step history.
--
-- Scope: tables, indexes, foreign keys, and the one active trigger - the objects that affect
-- real schema/data behavior. invent.hr also carries a set of legacy stored PROCEDUREs (DeviceAdd,
-- UserAdd, SensorDataPush, etc.) predating the EF Core rewrite; grep confirms no C# code calls
-- them anymore (the Dal method names of the same name are independent, plain-EF implementations),
-- and at least one (DeviceAdd) references columns that no longer exist after the farm-hierarchy
-- rename. They are dead weight for a fresh EF-managed schema and are intentionally NOT reproduced
-- here. The live invent.hr database itself was left untouched - this omission only affects this
-- file.
--
-- Apply to an empty database: `SOURCE baseline.sql;` (or via a MySQL/MariaDB client with the
-- target schema selected). Safe only against an EMPTY database - this is a from-scratch CREATE,
-- not an idempotent patch like the old migration files.

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

CREATE TABLE `auditLog` (
  `IDAuditLog` int(11) NOT NULL AUTO_INCREMENT,
  `TimestampUtc` datetime(6) NOT NULL,
  `TenantID` int(11) DEFAULT NULL,
  `ActorUserID` int(11) DEFAULT NULL,
  `ActorEmail` varchar(255) DEFAULT NULL,
  `Action` varchar(100) NOT NULL,
  `TargetType` varchar(50) DEFAULT NULL,
  `TargetId` varchar(50) DEFAULT NULL,
  `Details` text DEFAULT NULL,
  PRIMARY KEY (`IDAuditLog`),
  KEY `ix_auditLog_tenant_timestamp` (`TenantID`,`TimestampUtc`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `controllerData` (
  `IDControllerData` int(11) NOT NULL AUTO_INCREMENT,
  `DeviceID` int(11) NOT NULL,
  `TenantID` int(11) NOT NULL,
  `RelayFunction` int(11) NOT NULL,
  `IsOn` tinyint(1) NOT NULL,
  `DateChanged` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`IDControllerData`),
  UNIQUE KEY `ux_controllerData_device_relayFunction` (`DeviceID`,`RelayFunction`),
  CONSTRAINT `fk_controllerData_device` FOREIGN KEY (`DeviceID`) REFERENCES `device` (`IDDevice`) ON DELETE NO ACTION ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `device` (
  `IDDevice` int(11) NOT NULL AUTO_INCREMENT,
  `TenantID` int(11) DEFAULT NULL,
  `DeviceRoleID` int(11) DEFAULT 0,
  `DeviceFarmUnitID` int(11) DEFAULT NULL,
  `DeviceFarmUnitZoneID` int(11) DEFAULT NULL,
  `DeviceConfigBaseboardID` int(11) DEFAULT NULL,
  `DeviceConfigControllerID` int(11) DEFAULT NULL,
  `DeviceConfigSensorID` int(11) DEFAULT NULL,
  `DeviceTypeServiceID` int(11) DEFAULT 1 COMMENT 'HTTP OR MQTT',
  `DeviceName` varchar(128) DEFAULT NULL,
  `MacAddress` varchar(64) DEFAULT NULL,
  `ApiId` varchar(128) NOT NULL,
  `ApiKey` varchar(128) NOT NULL,
  `ServicePoint` varchar(200) DEFAULT 'api.agrumy.com',
  `ServicePublicKey` mediumtext DEFAULT NULL,
  `SleepSeconds` int(11) DEFAULT 60,
  `SleepDeepEnabled` tinyint(1) DEFAULT 0,
  `LoRaGatewayEnabled` tinyint(1) DEFAULT 0,
  `DeviceSensorEnabled` tinyint(1) DEFAULT 0,
  `DeviceControllerEnabled` tinyint(1) DEFAULT 0,
  `BatteryEnabled` tinyint(1) DEFAULT 0,
  `Enabled` tinyint(1) DEFAULT 0,
  `Debug` tinyint(1) DEFAULT 1,
  `Reboot` tinyint(1) DEFAULT 0,
  `Reset` tinyint(1) DEFAULT 0,
  `FirmwareUpdate` tinyint(1) DEFAULT 0,
  `ConfigVersion` int(11) DEFAULT 0,
  `DateCreated` datetime DEFAULT current_timestamp(),
  `DateModified` datetime DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  `CommandVersion` int(11) NOT NULL DEFAULT 0,
  `FirmwareTargetVersion` varchar(20) DEFAULT NULL,
  `IsGateway` tinyint(1) NOT NULL DEFAULT 0,
  `GatewayProfile` int(11) DEFAULT NULL,
  `LastFullConfigSentAt` datetime DEFAULT NULL,
  `ManualDeviceTypeID` int(11) DEFAULT NULL,
  `LoRaPrivateKeyHex` varchar(64) DEFAULT NULL,
  `LoRaLastUplinkCounter` bigint(20) DEFAULT NULL,
  `Deleted` tinyint(1) NOT NULL DEFAULT 0,
  `DeletedAtUtc` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`IDDevice`),
  UNIQUE KEY `ApiID_UNIQUE` (`ApiId`),
  UNIQUE KEY `MacAddress_TenantID_UNIQUE` (`MacAddress`,`TenantID`),
  KEY `fk_device_deviceRole_idx` (`DeviceRoleID`),
  KEY `fk_device_deviceConfigController_idx` (`DeviceConfigControllerID`),
  KEY `fk_device_deviceConfigSensor_idx` (`DeviceConfigSensorID`),
  KEY `fk_device_unit_idx` (`DeviceFarmUnitID`),
  KEY `fk_device_tenant_idx` (`TenantID`),
  KEY `fk_device_deviceTypeService_idx` (`DeviceTypeServiceID`),
  KEY `fk_device_deviceConfigController_idx1` (`DeviceConfigBaseboardID`),
  KEY `FK_device_deviceType_ManualDeviceTypeID` (`ManualDeviceTypeID`),
  CONSTRAINT `FK_device_deviceType_ManualDeviceTypeID` FOREIGN KEY (`ManualDeviceTypeID`) REFERENCES `deviceType` (`IDDeviceType`),
  CONSTRAINT `fk_device_deviceConfigController` FOREIGN KEY (`DeviceConfigControllerID`) REFERENCES `deviceConfigController` (`IDDeviceConfigController`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `fk_device_deviceConfigSensor` FOREIGN KEY (`DeviceConfigSensorID`) REFERENCES `deviceConfigSensor` (`IDDeviceConfigSensor`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `fk_device_deviceRole` FOREIGN KEY (`DeviceRoleID`) REFERENCES `deviceRole` (`IDDeviceRole`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `fk_device_deviceTypeService` FOREIGN KEY (`DeviceTypeServiceID`) REFERENCES `deviceTypeService` (`IDDeviceTypeService`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `fk_device_farmUnit` FOREIGN KEY (`DeviceFarmUnitID`) REFERENCES `deviceFarmUnit` (`IDDeviceFarmUnit`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `fk_device_tenant` FOREIGN KEY (`TenantID`) REFERENCES `tenant` (`IDTenant`) ON DELETE NO ACTION ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `deviceCommand` (
  `IDDeviceCommand` int(11) NOT NULL AUTO_INCREMENT,
  `DeviceID` int(11) NOT NULL,
  `ActionType` int(11) NOT NULL,
  `Status` int(11) NOT NULL DEFAULT 0,
  `IssuedAt` datetime(6) NOT NULL,
  `ExpiresAt` datetime(6) NOT NULL,
  `ExecutedAt` datetime(6) DEFAULT NULL,
  `ActiveKey` int(11) DEFAULT NULL,
  `Payload` text DEFAULT NULL,
  PRIMARY KEY (`IDDeviceCommand`),
  UNIQUE KEY `ux_deviceCommand_device_activekey` (`DeviceID`,`ActiveKey`),
  KEY `ix_deviceCommand_device_status` (`DeviceID`,`Status`),
  CONSTRAINT `fk_deviceCommand_device` FOREIGN KEY (`DeviceID`) REFERENCES `device` (`IDDevice`) ON DELETE NO ACTION ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `deviceConfigBaseboard` (
  `IDDeviceConfigBaseboard` int(11) NOT NULL AUTO_INCREMENT,
  `DeviceConfigBaseboardName` varchar(255) NOT NULL,
  `POWER_RAIL_PRIMARY` int(11) DEFAULT NULL,
  `POWER_RAIL_SECONDARY` int(11) DEFAULT NULL,
  `STATUS_POWER` int(11) DEFAULT NULL,
  `STATUS_SENSOR` int(11) DEFAULT NULL,
  `STATUS_ERROR` int(11) DEFAULT NULL,
  `RELAY_1` int(11) DEFAULT NULL,
  `RELAY_2` int(11) DEFAULT NULL,
  `RELAY_3` int(11) DEFAULT NULL,
  `RELAY_4` int(11) DEFAULT NULL,
  `RELAY_5` int(11) DEFAULT NULL,
  `RELAY_6` int(11) DEFAULT NULL,
  `RELAY_7` int(11) DEFAULT NULL,
  `RELAY_8` int(11) DEFAULT NULL,
  `SENSOR_DHT` int(11) DEFAULT NULL,
  `SENSOR_TEMPSOIL` int(11) DEFAULT NULL,
  `SENSOR_MOIST` int(11) DEFAULT NULL,
  `SENSOR_WaterLevel` int(11) DEFAULT NULL,
  `SENSOR_PH` int(11) DEFAULT NULL,
  `COM_RX` int(11) DEFAULT NULL,
  `COM_TX` int(11) DEFAULT NULL,
  PRIMARY KEY (`IDDeviceConfigBaseboard`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE `deviceConfigController` (
  `IDDeviceConfigController` int(11) NOT NULL AUTO_INCREMENT,
  `TempLow` double DEFAULT 5,
  `TempHigh` double DEFAULT 30,
  `HumidLow` double DEFAULT 35,
  `HumidHigh` double DEFAULT 80,
  `MoistLow` double DEFAULT 20,
  `MoistHigh` double DEFAULT 80,
  `LightLow` double DEFAULT 20,
  `LightHigh` double DEFAULT 100,
  `WaterLow` double DEFAULT 10,
  `WaterHigh` double DEFAULT 95,
  `VentilationIntervalEnabled` tinyint(1) DEFAULT 0,
  `VentilationInterval` int(11) DEFAULT 0,
  `VentilationIntervalLength` int(11) DEFAULT NULL,
  `LightIntervalEnabled` tinyint(1) DEFAULT 0,
  `LightInterval` int(11) DEFAULT 0,
  `LightIntervalLength` int(11) DEFAULT NULL,
  `HeatingIntervalEnabled` tinyint(1) DEFAULT 0,
  `HeatingInterval` int(11) DEFAULT 0,
  `HeatingIntervalLength` int(11) DEFAULT NULL,
  `WaterPumpIntervalEnabled` tinyint(1) DEFAULT 0,
  `WaterPumpInterval` int(11) DEFAULT 0,
  `WaterPumpIntervalLength` int(11) DEFAULT NULL,
  `RelayEnabled` tinyint(1) DEFAULT 0,
  `WaterLevelHysteresis` double DEFAULT NULL,
  `TemperatureHysteresis` double DEFAULT NULL,
  `HumidityHysteresis` double DEFAULT NULL,
  `LightHysteresis` double DEFAULT NULL,
  `WaterPumpMaxRunSeconds` int(11) DEFAULT NULL,
  `WaterPumpCooldownSeconds` int(11) DEFAULT NULL,
  PRIMARY KEY (`IDDeviceConfigController`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `deviceConfigControllerRelay` (
  `IDDeviceConfigController` int(11) NOT NULL,
  `Slot` int(11) NOT NULL,
  `RelayFunction` int(11) NOT NULL,
  PRIMARY KEY (`IDDeviceConfigController`,`Slot`),
  KEY `fk_deviceConfigControllerRelay_relayFunction` (`RelayFunction`),
  CONSTRAINT `fk_deviceConfigControllerRelay_controller` FOREIGN KEY (`IDDeviceConfigController`) REFERENCES `deviceConfigController` (`IDDeviceConfigController`) ON DELETE CASCADE,
  CONSTRAINT `fk_deviceConfigControllerRelay_relayFunction` FOREIGN KEY (`RelayFunction`) REFERENCES `deviceTypeRelay` (`IDDeviceTypeRelay`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `deviceConfigSensor` (
  `IDDeviceConfigSensor` int(11) NOT NULL AUTO_INCREMENT,
  `SensorBattery` int(11) DEFAULT 0,
  `SensorTemp` int(11) DEFAULT 0,
  `SensorTempSoil` int(11) DEFAULT 0,
  `SensorHumid` int(11) DEFAULT 0,
  `SensorMoist` int(11) DEFAULT 0,
  `SensorLight` int(11) DEFAULT 0,
  `SensorCo2` int(11) DEFAULT 0,
  `SensorTvoc` int(11) DEFAULT 0,
  `SensorBarometer` int(11) DEFAULT 0,
  `SensorPH` int(11) DEFAULT 0,
  `SensorRainLevel` int(11) DEFAULT 0,
  `SensorWaterLevel` int(11) DEFAULT 0,
  `SensorWind` int(11) DEFAULT 0,
  `BatteryDividerR1` double DEFAULT NULL,
  `BatteryDividerR2` double DEFAULT NULL,
  `SensorEc` int(11) DEFAULT 0,
  `SensorWeight` int(11) DEFAULT 0,
  `WeightCalibrationFactor` double DEFAULT NULL,
  `EcCalibrationSlope` double DEFAULT NULL,
  `EcCalibrationOffset` double DEFAULT NULL,
  PRIMARY KEY (`IDDeviceConfigSensor`),
  KEY `fk_deviceConfigSensor_deviceTypeSensor_battery_idx` (`SensorBattery`),
  KEY `fk_deviceConfigSensor_deviceTypeSensor_temp_idx` (`SensorTemp`),
  KEY `fk_deviceConfigSensor_deviceTypeSensor_tempSoil_idx` (`SensorTempSoil`),
  KEY `fk_deviceConfigSensor_deviceTypeSensor_humid_idx` (`SensorHumid`),
  KEY `fk_deviceConfigSensor_deviceTypeSensor_moist_idx` (`SensorMoist`),
  KEY `fk_deviceConfigSensor_deviceTypeSensor_light_idx` (`SensorLight`),
  KEY `fk_deviceConfigSensor_deviceTypeSensor_co2_idx` (`SensorCo2`),
  KEY `fk_deviceConfigSensor_deviceTypeSensor_tvoc_idx` (`SensorTvoc`),
  KEY `fk_deviceConfigSensor_deviceTypeSensor_barometer_idx` (`SensorBarometer`),
  KEY `fk_deviceConfigSensor_deviceTypeSensor_ph_idx` (`SensorPH`),
  KEY `fk_deviceConfigSensor_deviceTypeSensor_rainLevel_idx` (`SensorRainLevel`),
  KEY `fk_deviceConfigSensor_deviceTypeSensor_waterLevel_idx` (`SensorWaterLevel`),
  KEY `fk_deviceConfigSensor_deviceTypeSensor_wind_idx` (`SensorWind`),
  KEY `FK_deviceConfigSensor_deviceTypeSensor_SensorEc` (`SensorEc`),
  KEY `FK_deviceConfigSensor_deviceTypeSensor_SensorWeight` (`SensorWeight`),
  CONSTRAINT `FK_deviceConfigSensor_deviceTypeSensor_SensorEc` FOREIGN KEY (`SensorEc`) REFERENCES `deviceTypeSensor` (`IDDeviceTypeSensor`),
  CONSTRAINT `FK_deviceConfigSensor_deviceTypeSensor_SensorWeight` FOREIGN KEY (`SensorWeight`) REFERENCES `deviceTypeSensor` (`IDDeviceTypeSensor`),
  CONSTRAINT `fk_deviceConfigSensor_deviceTypeSensor_barometer` FOREIGN KEY (`SensorBarometer`) REFERENCES `deviceTypeSensor` (`IDDeviceTypeSensor`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `fk_deviceConfigSensor_deviceTypeSensor_battery` FOREIGN KEY (`SensorBattery`) REFERENCES `deviceTypeSensor` (`IDDeviceTypeSensor`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `fk_deviceConfigSensor_deviceTypeSensor_co2` FOREIGN KEY (`SensorCo2`) REFERENCES `deviceTypeSensor` (`IDDeviceTypeSensor`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `fk_deviceConfigSensor_deviceTypeSensor_humid` FOREIGN KEY (`SensorHumid`) REFERENCES `deviceTypeSensor` (`IDDeviceTypeSensor`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `fk_deviceConfigSensor_deviceTypeSensor_light` FOREIGN KEY (`SensorLight`) REFERENCES `deviceTypeSensor` (`IDDeviceTypeSensor`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `fk_deviceConfigSensor_deviceTypeSensor_moist` FOREIGN KEY (`SensorMoist`) REFERENCES `deviceTypeSensor` (`IDDeviceTypeSensor`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `fk_deviceConfigSensor_deviceTypeSensor_ph` FOREIGN KEY (`SensorPH`) REFERENCES `deviceTypeSensor` (`IDDeviceTypeSensor`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `fk_deviceConfigSensor_deviceTypeSensor_rainLevel` FOREIGN KEY (`SensorRainLevel`) REFERENCES `deviceTypeSensor` (`IDDeviceTypeSensor`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `fk_deviceConfigSensor_deviceTypeSensor_temp` FOREIGN KEY (`SensorTemp`) REFERENCES `deviceTypeSensor` (`IDDeviceTypeSensor`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `fk_deviceConfigSensor_deviceTypeSensor_tempSoil` FOREIGN KEY (`SensorTempSoil`) REFERENCES `deviceTypeSensor` (`IDDeviceTypeSensor`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `fk_deviceConfigSensor_deviceTypeSensor_tvoc` FOREIGN KEY (`SensorTvoc`) REFERENCES `deviceTypeSensor` (`IDDeviceTypeSensor`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `fk_deviceConfigSensor_deviceTypeSensor_waterLevel` FOREIGN KEY (`SensorWaterLevel`) REFERENCES `deviceTypeSensor` (`IDDeviceTypeSensor`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `fk_deviceConfigSensor_deviceTypeSensor_wind` FOREIGN KEY (`SensorWind`) REFERENCES `deviceTypeSensor` (`IDDeviceTypeSensor`) ON DELETE NO ACTION ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='x';

CREATE TABLE `deviceDiagnostic` (
  `DeviceID` int(11) NOT NULL,
  `TenantID` int(11) DEFAULT NULL,
  `LastSeenAt` datetime(6) DEFAULT NULL,
  `UptimeSeconds` bigint(20) DEFAULT NULL,
  `RssiDbm` int(11) DEFAULT NULL,
  `FreeHeapBytes` bigint(20) DEFAULT NULL,
  `FirmwareVersion` varchar(20) DEFAULT NULL,
  `OfflineNotifiedAt` datetime(6) DEFAULT NULL,
  `Board` varchar(40) DEFAULT NULL,
  `LowBatteryNotifiedAt` datetime(6) DEFAULT NULL,
  `DeviceTypeID` int(11) DEFAULT NULL,
  PRIMARY KEY (`DeviceID`),
  KEY `FK_deviceDiagnostic_deviceType_DeviceTypeID` (`DeviceTypeID`),
  CONSTRAINT `FK_deviceDiagnostic_deviceType_DeviceTypeID` FOREIGN KEY (`DeviceTypeID`) REFERENCES `deviceType` (`IDDeviceType`),
  CONSTRAINT `FK_deviceDiagnostic_device_DeviceID` FOREIGN KEY (`DeviceID`) REFERENCES `device` (`IDDevice`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `deviceDiscoveryReport` (
  `IDReport` int(11) NOT NULL AUTO_INCREMENT,
  `ScanningDeviceID` int(11) NOT NULL,
  `DiscoveredApMac` varchar(64) NOT NULL,
  `Rssi` int(11) DEFAULT NULL,
  `DateReported` datetime(6) NOT NULL,
  PRIMARY KEY (`IDReport`),
  KEY `ix_deviceDiscoveryReport_apMac` (`DiscoveredApMac`),
  KEY `fk_deviceDiscoveryReport_device` (`ScanningDeviceID`),
  CONSTRAINT `fk_deviceDiscoveryReport_device` FOREIGN KEY (`ScanningDeviceID`) REFERENCES `device` (`IDDevice`) ON DELETE NO ACTION ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `deviceFarm` (
  `IDDeviceFarm` int(11) NOT NULL AUTO_INCREMENT,
  `TenantID` int(11) DEFAULT NULL,
  `DeviceFarmName` varchar(100) DEFAULT NULL,
  `Deleted` tinyint(1) NOT NULL DEFAULT 0,
  `DeletedAtUtc` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`IDDeviceFarm`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `deviceFarmUnit` (
  `IDDeviceFarmUnit` int(11) NOT NULL,
  `TenantID` int(11) DEFAULT NULL,
  `DeviceFarmUnitName` varchar(100) DEFAULT NULL,
  `DeviceFarmID` int(11) DEFAULT NULL,
  `ZoneEnabled` bit(1) DEFAULT b'0',
  `Deleted` tinyint(1) NOT NULL DEFAULT 0,
  `DeletedAtUtc` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`IDDeviceFarmUnit`),
  KEY `ix_deviceFarmUnit_farm` (`DeviceFarmID`),
  CONSTRAINT `fk_deviceFarmUnit_deviceFarm` FOREIGN KEY (`DeviceFarmID`) REFERENCES `deviceFarm` (`IDDeviceFarm`) ON DELETE NO ACTION ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `deviceFarmUnitZone` (
  `IDDeviceFarmUnitZone` int(11) NOT NULL,
  `TenantID` int(11) DEFAULT NULL,
  `DeviceFarmUnitID` int(11) NOT NULL,
  `DeviceFarmUnitZoneName` varchar(120) DEFAULT NULL,
  `WaterPumpMaxRunSeconds` int(11) DEFAULT NULL,
  `WaterPumpCooldownSeconds` int(11) DEFAULT NULL,
  `SkipWaterPumpWhenRainPredicted` tinyint(1) NOT NULL DEFAULT 0,
  `TankCapacityLiters` double DEFAULT NULL,
  `WaterLevelRawEmpty` int(11) DEFAULT NULL,
  `WaterLevelRawFull` int(11) DEFAULT NULL,
  `TankRefillNotifiedAt` datetime(6) DEFAULT NULL,
  `HeatingMaxRunSeconds` int(11) DEFAULT NULL,
  `VentilationMaxRunSeconds` int(11) DEFAULT NULL,
  `WaterPumpMinLevel` double DEFAULT NULL,
  `Deleted` tinyint(1) NOT NULL DEFAULT 0,
  `DeletedAtUtc` datetime(6) DEFAULT NULL,
  `DashboardWidgetsJson` text DEFAULT NULL,
  PRIMARY KEY (`IDDeviceFarmUnitZone`),
  KEY `fk_deviceFarmUnitZone_deviceFarmUnit` (`DeviceFarmUnitID`),
  CONSTRAINT `fk_deviceFarmUnitZone_deviceFarmUnit` FOREIGN KEY (`DeviceFarmUnitID`) REFERENCES `deviceFarmUnit` (`IDDeviceFarmUnit`) ON DELETE NO ACTION ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='		';

CREATE TABLE `deviceFarmUnitZoneRule` (
  `IDDeviceFarmUnitZoneRule` int(11) NOT NULL AUTO_INCREMENT,
  `DeviceFarmUnitZoneID` int(11) DEFAULT NULL,
  `RelayFunction` int(11) DEFAULT NULL,
  `TenantID` int(11) NOT NULL DEFAULT 0,
  `DeviceFarmID` int(11) DEFAULT NULL,
  `DeviceFarmUnitID` int(11) DEFAULT NULL,
  `ActionType` int(11) NOT NULL DEFAULT 1,
  `Name` varchar(100) NOT NULL DEFAULT '',
  `Description` text DEFAULT NULL,
  `NotificationSubject` text DEFAULT NULL,
  `NotificationBody` text DEFAULT NULL,
  `RootConditionJson` text NOT NULL,
  `IsSafetyRule` tinyint(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (`IDDeviceFarmUnitZoneRule`),
  KEY `ix_deviceFarmUnitZoneRule_zone` (`DeviceFarmUnitZoneID`),
  KEY `ix_deviceFarmUnitZoneRule_unit` (`DeviceFarmUnitID`),
  KEY `ix_deviceFarmUnitZoneRule_tenant` (`TenantID`),
  KEY `ix_deviceFarmUnitZoneRule_farm` (`DeviceFarmID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `deviceFirmware` (
  `IDDeviceFirmware` int(11) NOT NULL AUTO_INCREMENT,
  `DeviceTypeID` int(11) DEFAULT NULL,
  `Version` varchar(20) DEFAULT NULL,
  `Url` mediumtext DEFAULT NULL,
  `DateAdded` datetime DEFAULT current_timestamp(),
  `Board` varchar(40) DEFAULT NULL,
  `Source` int(11) NOT NULL DEFAULT 0,
  `FileName` varchar(120) DEFAULT NULL,
  `SizeBytes` bigint(20) DEFAULT NULL,
  `Sha256` varchar(64) DEFAULT NULL,
  `PublishedAt` datetime(6) DEFAULT NULL,
  `FullImageFileName` varchar(120) DEFAULT NULL,
  `FullImageUrl` mediumtext DEFAULT NULL,
  `FullImageSizeBytes` bigint(20) DEFAULT NULL,
  `FullImageSha256` varchar(64) DEFAULT NULL,
  PRIMARY KEY (`IDDeviceFirmware`),
  KEY `ix_deviceFirmware_board_source` (`Board`,`Source`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `deviceManualOverride` (
  `IDDeviceManualOverride` int(11) NOT NULL AUTO_INCREMENT,
  `DeviceID` int(11) NOT NULL,
  `TenantID` int(11) NOT NULL,
  `RelayFunction` int(11) NOT NULL,
  `Mode` int(11) NOT NULL,
  `StartedAtUtc` datetime(6) NOT NULL,
  `ExpiresAtUtc` datetime(6) NOT NULL,
  `TargetMetric` int(11) DEFAULT NULL,
  `TargetThreshold` double DEFAULT NULL,
  `TargetHysteresis` double DEFAULT NULL,
  PRIMARY KEY (`IDDeviceManualOverride`),
  UNIQUE KEY `ux_deviceManualOverride_device_relayfunction` (`DeviceID`,`RelayFunction`),
  CONSTRAINT `FK_deviceManualOverride_device_DeviceID` FOREIGN KEY (`DeviceID`) REFERENCES `device` (`IDDevice`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `deviceRole` (
  `IDDeviceRole` int(11) NOT NULL AUTO_INCREMENT,
  `DeviceRoleName` varchar(100) DEFAULT NULL,
  `SensorEnabled` tinyint(1) DEFAULT NULL,
  `ControllerEnabled` tinyint(1) DEFAULT NULL,
  PRIMARY KEY (`IDDeviceRole`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `deviceScheduleSlot` (
  `IDDeviceScheduleSlot` int(11) NOT NULL AUTO_INCREMENT,
  `DeviceConfigControllerID` int(11) NOT NULL,
  `RelayFunction` int(11) NOT NULL,
  `DaysOfWeek` int(11) NOT NULL DEFAULT 0,
  `Start` int(11) NOT NULL DEFAULT 0,
  `Duration` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`IDDeviceScheduleSlot`),
  KEY `fk_deviceScheduleSlot_deviceConfigController_idx` (`DeviceConfigControllerID`),
  CONSTRAINT `fk_deviceScheduleSlot_deviceConfigController` FOREIGN KEY (`DeviceConfigControllerID`) REFERENCES `deviceConfigController` (`IDDeviceConfigController`) ON DELETE NO ACTION ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `deviceSimulation` (
  `DeviceID` int(11) NOT NULL,
  `Enabled` tinyint(1) NOT NULL,
  `Temperature` double DEFAULT NULL,
  `SoilTemperature` double DEFAULT NULL,
  `Humidity` double DEFAULT NULL,
  `Battery` int(11) DEFAULT NULL,
  `Moisture` int(11) DEFAULT NULL,
  `Light` int(11) DEFAULT NULL,
  `Co2` int(11) DEFAULT NULL,
  `Tvoc` int(11) DEFAULT NULL,
  `Barometer` double DEFAULT NULL,
  `LiquidPH` double DEFAULT NULL,
  `RainLevel` int(11) DEFAULT NULL,
  `WaterLevel` int(11) DEFAULT NULL,
  `Wind` int(11) DEFAULT NULL,
  PRIMARY KEY (`DeviceID`),
  CONSTRAINT `FK_deviceSimulation_device_DeviceID` FOREIGN KEY (`DeviceID`) REFERENCES `device` (`IDDevice`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `deviceType` (
  `IDDeviceType` int(11) NOT NULL AUTO_INCREMENT,
  `Kit` varchar(64) NOT NULL,
  `ControllerCapable` tinyint(1) NOT NULL DEFAULT 0,
  `PinoutJson` longtext DEFAULT NULL,
  PRIMARY KEY (`IDDeviceType`),
  UNIQUE KEY `ux_deviceType_kit` (`Kit`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `deviceTypeRelay` (
  `IDDeviceTypeRelay` int(11) NOT NULL,
  `RelayName` varchar(128) DEFAULT NULL,
  PRIMARY KEY (`IDDeviceTypeRelay`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `deviceTypeSensor` (
  `IDDeviceTypeSensor` int(11) NOT NULL,
  `SensorName` varchar(128) DEFAULT NULL,
  `SensorDescription` text DEFAULT NULL,
  `Battery` int(11) DEFAULT 0,
  `Temperature` int(11) DEFAULT 0,
  `TemperatureSoil` int(11) DEFAULT 0,
  `Humidity` int(11) DEFAULT 0,
  `Moisture` int(11) DEFAULT 0,
  `Light` int(11) DEFAULT 0,
  `Co2` int(11) DEFAULT 0,
  `Tvoc` int(11) DEFAULT 0,
  `Barometer` int(11) DEFAULT 0,
  `WaterPH` int(11) DEFAULT 0,
  `WaterTankLevel` int(11) DEFAULT 0,
  `RainLevel` int(11) DEFAULT 0,
  `Wind` int(11) DEFAULT 0,
  `Ec` int(11) DEFAULT NULL,
  `Weight` int(11) DEFAULT NULL,
  PRIMARY KEY (`IDDeviceTypeSensor`),
  KEY `fk_deviceTypeSensor_deviceConfigSensor_battery_idx` (`Battery`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `deviceTypeService` (
  `IDDeviceTypeService` int(11) NOT NULL,
  `ServiceType` varchar(5) DEFAULT 'HTTPS',
  PRIMARY KEY (`IDDeviceTypeService`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `deviceVirtual` (
  `DeviceID` int(11) NOT NULL,
  `DateCreated` datetime(6) DEFAULT current_timestamp(6),
  PRIMARY KEY (`DeviceID`),
  CONSTRAINT `FK_virtualDevice_device_DeviceID` FOREIGN KEY (`DeviceID`) REFERENCES `device` (`IDDevice`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `eventDevice` (
  `IDEventDevice` int(11) NOT NULL AUTO_INCREMENT,
  `DeviceID` int(11) NOT NULL,
  `EventID` int(11) NOT NULL,
  `Date` datetime DEFAULT NULL,
  `Message` mediumtext DEFAULT NULL,
  `TenantID` int(11) NOT NULL DEFAULT 0,
  `AcknowledgedAt` datetime DEFAULT NULL,
  PRIMARY KEY (`IDEventDevice`),
  KEY `ix_eventDevice_device_date` (`DeviceID`,`Date`),
  KEY `FK_eventDevice_eventType_EventID` (`EventID`),
  CONSTRAINT `FK_eventDevice_eventType_EventID` FOREIGN KEY (`EventID`) REFERENCES `eventType` (`IDEventType`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `eventService` (
  `IDEventService` int(11) NOT NULL AUTO_INCREMENT,
  `ServiceID` int(11) NOT NULL,
  `EventID` int(11) NOT NULL,
  `Date` datetime DEFAULT NULL,
  `Message` mediumtext DEFAULT NULL,
  PRIMARY KEY (`IDEventService`),
  KEY `FK_eventService_eventType_EventID` (`EventID`),
  KEY `FK_eventService_deviceTypeService_ServiceID` (`ServiceID`),
  CONSTRAINT `FK_eventService_deviceTypeService_ServiceID` FOREIGN KEY (`ServiceID`) REFERENCES `deviceTypeService` (`IDDeviceTypeService`),
  CONSTRAINT `FK_eventService_eventType_EventID` FOREIGN KEY (`EventID`) REFERENCES `eventType` (`IDEventType`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `eventType` (
  `IDEventType` int(11) NOT NULL,
  `EventTypeName` varchar(64) DEFAULT NULL,
  PRIMARY KEY (`IDEventType`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `gatewayDeviceMapping` (
  `IDGatewayDeviceMapping` int(11) NOT NULL AUTO_INCREMENT,
  `IDGatewayDevice` int(11) NOT NULL,
  `DevEUI` varchar(16) NOT NULL,
  `IDDevice` int(11) NOT NULL,
  `DateCreated` datetime DEFAULT current_timestamp(),
  PRIMARY KEY (`IDGatewayDeviceMapping`),
  UNIQUE KEY `ux_gatewayDeviceMapping_gateway_deveui` (`IDGatewayDevice`,`DevEUI`),
  KEY `ix_gatewayDeviceMapping_IDDevice` (`IDDevice`),
  CONSTRAINT `fk_gatewayDeviceMapping_device` FOREIGN KEY (`IDDevice`) REFERENCES `device` (`IDDevice`),
  CONSTRAINT `fk_gatewayDeviceMapping_gateway` FOREIGN KEY (`IDGatewayDevice`) REFERENCES `device` (`IDDevice`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `ruleNotificationState` (
  `IDRuleNotificationState` int(11) NOT NULL AUTO_INCREMENT,
  `RuleID` int(11) NOT NULL,
  `DeviceFarmUnitZoneID` int(11) NOT NULL,
  `WasTrue` tinyint(1) NOT NULL DEFAULT 0,
  `LastFiredAtUtc` datetime DEFAULT NULL,
  PRIMARY KEY (`IDRuleNotificationState`),
  UNIQUE KEY `ux_ruleNotificationState_rule_zone` (`RuleID`,`DeviceFarmUnitZoneID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `sensorData` (
  `IDSensorData` int(11) NOT NULL AUTO_INCREMENT,
  `TenantID` int(11) NOT NULL,
  `DeviceID` int(11) NOT NULL,
  `DeviceFarmUnitID` int(11) DEFAULT NULL,
  `DeviceFarmUnitZoneID` int(11) DEFAULT NULL,
  `Battery` tinyint(1) DEFAULT NULL,
  `Temperature` double DEFAULT NULL,
  `SoilTemperature` double DEFAULT NULL,
  `Humidity` double DEFAULT NULL,
  `Moisture` tinyint(1) DEFAULT NULL,
  `Light` int(11) DEFAULT NULL,
  `Co2` int(11) DEFAULT NULL,
  `Tvoc` int(11) DEFAULT NULL,
  `Barometer` double DEFAULT NULL,
  `LiquidPH` double DEFAULT NULL,
  `RainLevel` int(11) DEFAULT NULL,
  `WaterLevel` tinyint(1) DEFAULT NULL,
  `Wind` int(11) DEFAULT NULL,
  `DateCreated` datetime DEFAULT NULL,
  `Ec` double DEFAULT NULL,
  `Weight` double DEFAULT NULL,
  PRIMARY KEY (`IDSensorData`),
  KEY `fk_sensorData_tenant_idx` (`TenantID`),
  KEY `fk_sensorData_device_idx` (`DeviceID`),
  KEY `fk_sensorData_deviceUnit_idx` (`DeviceFarmUnitID`),
  KEY `fk_sensorData_deviceUnitZone_idx` (`DeviceFarmUnitZoneID`),
  KEY `ix_sensorData_deviceFarmUnitZone_date` (`DeviceFarmUnitZoneID`,`DateCreated`),
  CONSTRAINT `fk_sensorData_device` FOREIGN KEY (`DeviceID`) REFERENCES `device` (`IDDevice`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `fk_sensorData_deviceFarmUnit` FOREIGN KEY (`DeviceFarmUnitID`) REFERENCES `deviceFarmUnit` (`IDDeviceFarmUnit`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `fk_sensorData_deviceFarmUnitZone` FOREIGN KEY (`DeviceFarmUnitZoneID`) REFERENCES `deviceFarmUnitZone` (`IDDeviceFarmUnitZone`) ON DELETE NO ACTION ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `sensorDataReport` (
  `IDSensorDataReport` int(11) NOT NULL AUTO_INCREMENT,
  `deviceID` int(11) DEFAULT NULL,
  `ReportName` varchar(128) DEFAULT NULL,
  `DateGenerated` datetime DEFAULT current_timestamp(),
  `sensorData` longtext DEFAULT NULL,
  PRIMARY KEY (`IDSensorDataReport`),
  KEY `fk_sensorDataReport_device_idx` (`deviceID`),
  CONSTRAINT `fk_sensorDataReport_device` FOREIGN KEY (`deviceID`) REFERENCES `device` (`IDDevice`) ON DELETE NO ACTION ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Bilo bi dobro praznit sensorData tablicu i jednom mjesecno predgenerirat podatke za sve uredjaje, mjesecno, godisnje.\\nhttps://stackoverflow.com/questions/33926532/get-uncommitted-data-in-mysql\\nSELECT * FROM TABLE_NAME WITH (nolock), da read ne radi lock nad tablicom za insert--ovako--SELECT JSON_ARRAYAGG(JSON_OBJECT(''firstname'', firstname, ''lastname'', lastname)) from agrumy.person;';

CREATE TABLE `serverConfig` (
  `IDServerConfig` int(11) NOT NULL,
  `JWTKey` mediumtext DEFAULT NULL,
  `PortHTTP` int(11) DEFAULT NULL,
  `PortHTTPS` int(11) DEFAULT 80,
  `WaterLevelHysteresis` double DEFAULT NULL,
  `TemperatureHysteresis` double DEFAULT NULL,
  `HumidityHysteresis` double DEFAULT NULL,
  `LightHysteresis` double DEFAULT NULL,
  `EventDedupeMinutes` int(11) DEFAULT NULL,
  `ActivationResendCooldownMinutes` int(11) DEFAULT NULL,
  `AllowSelfServiceTenantCreation` tinyint(1) DEFAULT NULL,
  `ScheduleTimeZone` varchar(64) DEFAULT NULL,
  `FirmwareSource` int(11) NOT NULL DEFAULT 0,
  `FirmwareGitHubRepository` varchar(200) DEFAULT NULL,
  `FirmwareCustomRepositoryUrl` varchar(500) DEFAULT NULL,
  `BatteryLowThreshold` double DEFAULT NULL,
  `BatteryLowHysteresis` double DEFAULT NULL,
  `WaterPumpMaxRunSeconds` int(11) DEFAULT NULL,
  `WaterPumpCooldownSeconds` int(11) DEFAULT NULL,
  `SensorDataRetentionDays` int(11) DEFAULT NULL,
  `WeatherLocationLat` double DEFAULT NULL,
  `WeatherLocationLon` double DEFAULT NULL,
  `WeatherPollIntervalMinutes` int(11) DEFAULT NULL,
  `WeatherRainSkipThreshold` double DEFAULT NULL,
  `WeatherRainPredicted` tinyint(1) NOT NULL DEFAULT 0,
  `WeatherCheckedAtUtc` datetime DEFAULT NULL,
  `MaxRulesPerZone` int(11) DEFAULT NULL,
  `TenantManagementEnabled` tinyint(1) NOT NULL DEFAULT 0,
  `FirmwareRefreshIntervalHours` int(11) DEFAULT NULL,
  `FirmwareLastRefreshedAtUtc` datetime DEFAULT NULL,
  `ProblemEventAlertsEnabled` tinyint(1) NOT NULL DEFAULT 1,
  `ProblemEventExpiryHours` int(11) NOT NULL DEFAULT 24,
  `GatewayEnabled` tinyint(1) NOT NULL DEFAULT 0,
  `GatewayMode` int(11) NOT NULL DEFAULT 0,
  `GatewayWaitWindowSeconds` int(11) NOT NULL DEFAULT 30,
  `PasswordMinLength` int(11) NOT NULL DEFAULT 0,
  `PasswordRequireComplexity` tinyint(1) NOT NULL DEFAULT 0,
  `ConfigHeartbeatHours` int(11) NOT NULL DEFAULT 24,
  `MqttTransportEnabled` tinyint(1) NOT NULL DEFAULT 0,
  `MqttBrokerHost` varchar(255) DEFAULT NULL,
  `MqttBrokerPort` int(11) NOT NULL DEFAULT 1883,
  `MqttUsername` varchar(255) DEFAULT NULL,
  `MqttPassword` varchar(512) DEFAULT NULL,
  `TankRefillThreshold` double DEFAULT NULL,
  `TankRefillHysteresis` double DEFAULT NULL,
  `EmailEnabled` tinyint(1) NOT NULL DEFAULT 0,
  `EmailHost` varchar(255) DEFAULT NULL,
  `EmailPort` int(11) NOT NULL DEFAULT 587,
  `EmailUseStartTls` tinyint(1) NOT NULL DEFAULT 1,
  `EmailUsername` varchar(255) DEFAULT NULL,
  `EmailPassword` varchar(512) DEFAULT NULL,
  `EmailFromAddress` varchar(255) DEFAULT NULL,
  `EmailFromName` varchar(100) NOT NULL DEFAULT 'Agrumy',
  `DevicePinValidMinutes` int(11) NOT NULL DEFAULT 60,
  `ArchiveEnabled` tinyint(1) NOT NULL DEFAULT 0,
  `ArchiveCutoffMode` int(11) NOT NULL DEFAULT 0,
  `ArchiveCustomCutoffDate` date DEFAULT NULL,
  `ArchiveCustomRollingDays` int(11) DEFAULT NULL,
  `ArchiveHost` varchar(255) DEFAULT NULL,
  `ArchivePort` int(11) DEFAULT 3306,
  `ArchiveDatabaseName` varchar(64) DEFAULT NULL,
  `ArchiveUsername` varchar(128) DEFAULT NULL,
  `ArchivePassword` varchar(512) DEFAULT NULL,
  `ArchiveLastRunAtUtc` datetime(6) DEFAULT NULL,
  `RecycleBinRetentionDays` int(11) DEFAULT 30,
  `PurgeOrphanedSensorDataScheduleEnabled` tinyint(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (`IDServerConfig`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `simulationSession` (
  `IDSimulationSession` int(11) NOT NULL AUTO_INCREMENT,
  `TenantID` int(11) NOT NULL,
  `Name` varchar(128) DEFAULT NULL,
  `StartedAtUtc` datetime(6) DEFAULT NULL,
  `ExpiresAtUtc` datetime(6) DEFAULT NULL,
  `StoppedAtUtc` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`IDSimulationSession`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `simulationSessionDevice` (
  `IDSimulationSession` int(11) NOT NULL,
  `DeviceID` int(11) NOT NULL,
  PRIMARY KEY (`IDSimulationSession`,`DeviceID`),
  KEY `FK_simulationSessionDevice_device` (`DeviceID`),
  CONSTRAINT `FK_simulationSessionDevice_device` FOREIGN KEY (`DeviceID`) REFERENCES `device` (`IDDevice`),
  CONSTRAINT `FK_simulationSessionDevice_session` FOREIGN KEY (`IDSimulationSession`) REFERENCES `simulationSession` (`IDSimulationSession`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `tenant` (
  `IDTenant` int(11) NOT NULL AUTO_INCREMENT,
  `TenantName` varchar(100) NOT NULL,
  `DateCreated` datetime DEFAULT current_timestamp(),
  `ScheduleTimeZone` varchar(64) DEFAULT NULL,
  `Latitude` double DEFAULT NULL,
  `Longitude` double DEFAULT NULL,
  `EmergencyStopActive` tinyint(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (`IDTenant`),
  UNIQUE KEY `Name_UNIQUE` (`TenantName`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='umjesto da se veze acc na profil, veze se tenant.';

CREATE TABLE `tenantWifiConfig` (
  `IDTenantWifiConfig` int(11) NOT NULL AUTO_INCREMENT,
  `TenantID` int(11) NOT NULL,
  `Ssid` varchar(32) NOT NULL,
  `Password` varchar(512) NOT NULL,
  `DateCreated` datetime DEFAULT current_timestamp(),
  PRIMARY KEY (`IDTenantWifiConfig`),
  KEY `ix_tenantWifiConfig_tenant` (`TenantID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `user` (
  `IDUser` int(11) NOT NULL AUTO_INCREMENT,
  `TenantID` int(11) NOT NULL DEFAULT 0,
  `Email` varchar(100) NOT NULL,
  `Username` varchar(100) DEFAULT NULL,
  `PwdHash` text DEFAULT NULL,
  `PwdSalt` varchar(128) DEFAULT NULL,
  `DevicePin` varchar(8) DEFAULT NULL,
  `FirstName` varchar(100) DEFAULT NULL,
  `LastName` varchar(100) DEFAULT NULL,
  `Phone` varchar(15) DEFAULT NULL COMMENT 'International standards can support up to 15 digits',
  `Enabled` tinyint(1) DEFAULT 0,
  `DateCreated` datetime DEFAULT current_timestamp(),
  `DateModified` datetime DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  `EmailVerified` tinyint(1) NOT NULL DEFAULT 0,
  `ActivationTokenHash` varchar(64) DEFAULT NULL,
  `ActivationTokenExpiresAt` datetime DEFAULT NULL,
  `ActivationLastSentAt` datetime DEFAULT NULL,
  `TimeZone` varchar(64) DEFAULT NULL,
  `DevicePinExpires` datetime DEFAULT NULL,
  `BootstrapSecretHash` text DEFAULT NULL,
  `BootstrapSecretSalt` varchar(128) DEFAULT NULL,
  `MustChangePassword` tinyint(1) NOT NULL DEFAULT 0,
  `TokensValidAfterUtc` datetime DEFAULT NULL,
  PRIMARY KEY (`IDUser`),
  UNIQUE KEY `email_UNIQUE` (`Email`),
  UNIQUE KEY `Username_UNIQUE` (`Username`),
  UNIQUE KEY `ActivationTokenHash_UNIQUE` (`ActivationTokenHash`),
  KEY `ix_user_tenant` (`TenantID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `userRefreshToken` (
  `IDRefreshToken` int(11) NOT NULL AUTO_INCREMENT,
  `UserID` int(11) NOT NULL,
  `TokenHash` varchar(64) NOT NULL,
  `ExpiresAt` datetime(6) NOT NULL,
  `CreatedAt` datetime(6) NOT NULL DEFAULT current_timestamp(6),
  `RevokedAt` datetime(6) DEFAULT NULL,
  `ReplacedByTokenHash` varchar(64) DEFAULT NULL,
  PRIMARY KEY (`IDRefreshToken`),
  UNIQUE KEY `TokenHash_UNIQUE` (`TokenHash`),
  KEY `ix_userRefreshToken_userID` (`UserID`),
  CONSTRAINT `FK_userRefreshToken_user_UserID` FOREIGN KEY (`UserID`) REFERENCES `user` (`IDUser`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `userRole` (
  `IDUserRole` int(11) NOT NULL AUTO_INCREMENT,
  `RoleName` varchar(45) DEFAULT NULL,
  `RoleScopeID` int(11) DEFAULT NULL,
  PRIMARY KEY (`IDUserRole`),
  KEY `fk_userRole_userRoleScope_idx` (`RoleScopeID`),
  CONSTRAINT `fk_userRole_userRoleScope` FOREIGN KEY (`RoleScopeID`) REFERENCES `userRoleScope` (`IDRoleScope`) ON DELETE NO ACTION ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `userRoleScope` (
  `IDRoleScope` int(11) NOT NULL AUTO_INCREMENT,
  `RoleScopeName` varchar(45) DEFAULT NULL,
  PRIMARY KEY (`IDRoleScope`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `userUserRole` (
  `UserID` int(11) NOT NULL,
  `UserRoleID` int(11) NOT NULL,
  PRIMARY KEY (`UserID`,`UserRoleID`),
  KEY `fk_userUserRole_userRole` (`UserRoleID`),
  CONSTRAINT `fk_userUserRole_user` FOREIGN KEY (`UserID`) REFERENCES `user` (`IDUser`) ON DELETE CASCADE,
  CONSTRAINT `fk_userUserRole_userRole` FOREIGN KEY (`UserRoleID`) REFERENCES `userRole` (`IDUserRole`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DELIMITER ;;
CREATE TRIGGER `sensorData_SetDateTimeOnNull` 
BEFORE INSERT ON `sensorData` 
FOR EACH ROW
BEGIN
    IF NEW.DateCreated IS NULL THEN
	SET NEW.DateCreated = CURRENT_TIMESTAMP();
    END IF;
END;
;;
DELIMITER ;

SET FOREIGN_KEY_CHECKS = 1;
