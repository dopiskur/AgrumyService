namespace Agrumy.Shared.Models
{
    /// Which kind of id ScopeID is - unlike Simulation's snapshot-at-add device list, an Experiment's membership is dynamic (whatever devices sit under the scope at evaluation time), so scope alone is enough to define it.
    public enum ExperimentScope
    {
        Farm = 1,
        Unit = 2,
        Zone = 3,
    }

    /// Long-term, real-device A/B testing of an alternate rule set - unlike Simulation Mode, this controls REAL devices with REAL relay/notification actions, and every sensor/controller push from a device under an active experiment is also mirrored into dataSensorExperiment/dataControllerExperiment for later comparison against its normal history. ExpiresAtUtc is optional (an experiment may run indefinitely); StoppedAtUtc null means still running.
    public class Experiment
    {
        [Microsoft.AspNetCore.Mvc.HiddenInput(DisplayValue = true)]
        public int? IDExperiment { get; set; }
        public int? TenantID { get; set; }
        public string Name { get; set; } = "";
        public ExperimentScope Scope { get; set; }
        public int ScopeID { get; set; }
        /// Resolved display name of whichever Farm/Unit/Zone ScopeID points at - not stored, filled in by EfExperimentRepository the same way SimulationGroup.ScopeName is.
        public string? ScopeName { get; set; }
        public DateTimeOffset? StartedAtUtc { get; set; }
        public DateTimeOffset? ExpiresAtUtc { get; set; }
        public DateTimeOffset? StoppedAtUtc { get; set; }
    }

    public class ExperimentCreateRequest
    {
        public string Name { get; set; } = "";
        public ExperimentScope Scope { get; set; }
        public int ScopeID { get; set; }
        /// Null means open-ended - unlike Simulation, an experiment has no mandatory hard cap.
        public DateTimeOffset? ExpiresAtUtc { get; set; }
    }

    /// One dual-written sensor push, same fields as SensorData plus which device reported it - the basic raw-data view's row shape (richer charting/analysis deferred to a follow-up).
    public class ExperimentSensorSample
    {
        public int DeviceID { get; set; }
        public string? DeviceName { get; set; }
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

    /// One dual-written relay state-change event - dataControllerExperiment is an append-only log (unlike the current-state-only dataController), so every flip during the experiment survives, not just the latest one.
    public class ExperimentControllerEvent
    {
        public int DeviceID { get; set; }
        public string? DeviceName { get; set; }
        public RelayFunction RelayFunction { get; set; }
        public bool IsOn { get; set; }
        public DateTimeOffset? DateCreated { get; set; }
    }
}
