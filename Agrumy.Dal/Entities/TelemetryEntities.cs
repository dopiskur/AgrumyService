namespace Agrumy.Dal.Entities
{
    public class SensorDataRow
    {
        public int IDSensorData { get; set; }
        public int TenantID { get; set; }
        public int DeviceID { get; set; }
        public int? DeviceFarmUnitID { get; set; }
        public int? DeviceFarmUnitZoneID { get; set; }
        public int? SowingID { get; set; }
        public int? FarmParcelZoneID { get; set; }
        public int? Battery { get; set; }
        public double? Temperature { get; set; }
        public double? SoilTemperature { get; set; }
        public double? Humidity { get; set; }
        public int? Moisture { get; set; }
        public int? Light { get; set; }
        public int? Co2 { get; set; }
        public int? Tvoc { get; set; }
        public double? Barometer { get; set; }
        public double? LiquidPH { get; set; }
        public int? RainLevel { get; set; }
        public int? WaterLevel { get; set; }
        public int? Wind { get; set; }
        public double? Ec { get; set; }
        public double? Weight { get; set; }
        // Signal quality alongside the reading, not just a point-in-time diagnostic snapshot.
        public int? WifiRssiDbm { get; set; }
        public int? LoRaRssiDbm { get; set; }
        public int? LoRaSnrDb { get; set; }
        public DateTimeOffset? DateCreated { get; set; }
    }

    /// Current on/off state per (DeviceID, RelayFunction) - one row per pair, upserted on every real state change, not an append-only log. Same table/logic for a real device (physical relay) and a simulated one (calculated), the difference is only where the IsOn decision comes from.
    public class ControllerDataRow
    {
        public int IDControllerData { get; set; }
        public int DeviceID { get; set; }
        public int TenantID { get; set; }
        public int RelayFunction { get; set; }
        public bool IsOn { get; set; }
        public int? Percent { get; set; }
        public DateTimeOffset? DateChanged { get; set; }
    }

    /// A long-term, real-device rule experiment scoped to exactly one Farm/Unit/Zone. ExpiresAtUtc is optional (unlike simulationSession's mandatory 48h cap) - null means open-ended, runs until an explicit Stop. StoppedAtUtc null means still running.
    public class ExperimentRow
    {
        public int IDExperiment { get; set; }
        public int TenantID { get; set; }
        public string Name { get; set; } = "";
        /// 1=Farm, 2=Unit, 3=Zone (Agrumy.Shared.Models.ExperimentScope).
        public int Scope { get; set; }
        public int ScopeID { get; set; }
        public DateTimeOffset StartedAtUtc { get; set; }
        public DateTimeOffset? ExpiresAtUtc { get; set; }
        public DateTimeOffset? StoppedAtUtc { get; set; }
    }

    /// Append-only mirror of SensorDataRow, written alongside it whenever the pushing device is currently under an active experiment - kept permanently (no retention/optimize pass touches this table), so an experiment's own history survives independently of dataSensor's own retention/optimization.
    public class SensorDataExperimentRow
    {
        public int IDSensorDataExperiment { get; set; }
        public int IDExperiment { get; set; }
        public int TenantID { get; set; }
        public int DeviceID { get; set; }
        public double? Temperature { get; set; }
        public double? SoilTemperature { get; set; }
        public double? Humidity { get; set; }
        public int? Moisture { get; set; }
        public int? Light { get; set; }
        public int? Co2 { get; set; }
        public int? Tvoc { get; set; }
        public double? Barometer { get; set; }
        public double? LiquidPH { get; set; }
        public int? RainLevel { get; set; }
        public int? WaterLevel { get; set; }
        public int? Wind { get; set; }
        public DateTimeOffset? DateCreated { get; set; }
    }

    /// Unlike dataController (upserted current state, see ControllerDataRow's own remarks), this is a genuine append-only log - one row per push event, so an experiment's full history of relay flips survives, not just the latest state.
    public class ControllerDataExperimentRow
    {
        public int IDControllerDataExperiment { get; set; }
        public int IDExperiment { get; set; }
        public int TenantID { get; set; }
        public int DeviceID { get; set; }
        public int RelayFunction { get; set; }
        public bool IsOn { get; set; }
        public int? Percent { get; set; }
        public DateTimeOffset? DateCreated { get; set; }
    }

    /// Catalog of Agrumy.Shared.Models.DeviceEventType values, seeded 1:1 from that enum - backs EventDeviceRow.EventID so a future event type has one source of truth instead of a magic-number agreement.
    public class EventTypeRow
    {
        public int IDEventType { get; set; }
        public string EventTypeName { get; set; } = "";
    }

    public class EventDeviceRow
    {
        public int IDEventDevice { get; set; }
        public int DeviceID { get; set; }
        public int TenantID { get; set; } // Stamped from the authenticated device's identity at push time, never from a client-supplied value.
        public int EventID { get; set; }
        public DateTimeOffset? Date { get; set; }
        public string? Message { get; set; }
        public DateTimeOffset? AcknowledgedAt { get; set; } // Set once an admin dismisses this alert, stopping it counting toward Unit/Zone Orange status even inside the expiry window.
    }

    /// One row per tenant per calendar day - upserted, not appended, so a service restart re-ticking the same day just refreshes it instead of piling up duplicates.
    public class TenantUsageSnapshotRow
    {
        public int IDTenantUsageSnapshot { get; set; }
        public int TenantID { get; set; }
        public DateTimeOffset SnapshotDateUtc { get; set; }
        public int DeviceCount { get; set; }
        public long SensorDataRowCount { get; set; }
    }
}
