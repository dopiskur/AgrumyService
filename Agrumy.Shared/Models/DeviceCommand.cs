namespace Agrumy.Shared.Models
{
    public enum CommandActionType
    {
        Reboot = 1,
        ForceOTA = 2,
        ForceConfigSync = 3,
        ScanForDevices = 4,
        ProvisionDevice = 5,
        UpdateWifiCredentials = 6,
        DetectSensors = 7,
    }

    /// Device acknowledges before executing, so a command stuck at Acknowledged (never reaching Executed) means it took the command but crashed or lost power before confirming the outcome.
    public enum CommandStatus
    {
        Pending = 0,
        Acknowledged = 1,
        Executed = 2,
        Expired = 3,
    }

    /// Target resolved server-side to the device(s) implied by Zone/Unit; see CommandQueueService.IssueCommandAsync.
    public enum CommandTargetType
    {
        Device = 1,
        Zone = 2,
        Unit = 3,
    }

    /// Body of POST /api/DeviceCommand.
    public class IssueCommandRequest
    {
        public CommandTargetType TargetType { get; set; }
        public int TargetId { get; set; }
        public CommandActionType ActionType { get; set; }
    }

    public class DeviceCommand
    {
        public int IDDeviceCommand { get; set; }
        public int DeviceID { get; set; }
        public CommandActionType ActionType { get; set; }
        public CommandStatus Status { get; set; }
        public DateTimeOffset IssuedAt { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        public DateTimeOffset? ExecutedAt { get; set; }
        // Only ProvisionDevice sets this - see Agrumy.Shared.Models.DiscoveryProvisionPayload.
        public string? Payload { get; set; }
    }

    /// Minimal shape returned inside DeviceConfig during a device's regular config poll — not a separate endpoint.
    public class PendingCommand
    {
        public int IDDeviceCommand { get; set; }
        public CommandActionType ActionType { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        public string? Payload { get; set; }
    }

    /// Body of POST /api/Device/Command/Ack.
    public class CommandAckRequest
    {
        public int CommandId { get; set; }
    }

    /// Body of POST /api/Device/WifiUpdate - single device only, no Zone/Unit fan-out (each physical device has its own AP to switch, unlike a firmware/config push).
    public class DeviceWifiUpdateRequest
    {
        public int IdDevice { get; set; }
        public string Ssid { get; set; } = "";
        public string WifiPassword { get; set; } = "";
    }

    /// Body of POST /api/DeviceFarmUnit/{idDeviceFarmUnit}/WifiUpdate (roadmap #411) - same Ssid/WifiPassword as the single-device request, applied to every device under the unit.
    public class UnitWifiUpdateRequest
    {
        public string Ssid { get; set; } = "";
        public string WifiPassword { get; set; } = "";
    }

    /// One device's outcome from a unit-wide WiFi switch - Issued means the UpdateWifiCredentials command was queued (same #355 verify-then-persist mechanism then runs on the device itself, not tracked further here); Issued=false with Message set means IssueWifiUpdateCommandAsync's own dedup rejected it (already had one pending).
    public class UnitWifiUpdateDeviceResult
    {
        public int IDDevice { get; set; }
        public string? DeviceName { get; set; }
        public bool Issued { get; set; }
        public string? Message { get; set; }
    }

    public class UnitWifiUpdateResult
    {
        public int DeviceCount { get; set; }
        public int IssuedCount { get; set; }
        public IList<UnitWifiUpdateDeviceResult> Devices { get; set; } = [];
    }

    /// The DeviceCommand.Payload JSON for an UpdateWifiCredentials command - the target device connects to this AP, verifies it can still reach this same server before persisting it, and falls back to its previously-saved network otherwise (see ServiceController::switchWifiNetwork).
    public class WifiUpdatePayload
    {
        public string Ssid { get; set; } = "";
        public string WifiPassword { get; set; } = "";
    }

    /// One I2C address a DetectSensors scan found something answering at, with every SensorTypeIds constant whose driver's begin() also succeeded at that address - more than one candidate means a genuine ambiguous pair (e.g. Htu21Df/Si7021 both at 0x40), left for the admin to pick.
    public class DetectedSensorAddress
    {
        public int Address { get; set; }
        public List<int> Candidates { get; set; } = [];
    }

    /// Parsed from the device's CommandExecuted Message JSON for a DetectSensors command (see ServiceController::detectSensors) and persisted on Device.LastSensorDetectionResult; DetectedAt is set server-side at persist time, not device-reported, same reasoning as DeviceEvent.CreatedAt.
    public class DeviceSensorDetectionResult
    {
        public DateTimeOffset DetectedAt { get; set; }
        public List<DetectedSensorAddress> Addresses { get; set; } = [];
    }
}
