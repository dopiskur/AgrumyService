using Agrumy.Shared.Models;

namespace Agrumy.Web.ViewModels
{
    /// One function's row in _ManualActuateFunction.cshtml. Zone-scoped: Target mode is only offered when MaxRunSeconds is configured (it has no other self-cap), and Active/Stop reflects that one zone's real state. Unit/Farm-scoped (IsUnitLevel/IsFarmLevel): a fan-out trigger with no single "active" state to show - Target mode is always offered, individual zones lacking MaxRunSeconds are silently skipped server-side (Agrumy.Api.Commands.ManualActuateService), surfaced only in the post-submit message.
    public class ManualActuateFunctionViewModel
    {
        /// IDDeviceFarmUnitZone normally, IDDeviceFarmUnit when IsUnitLevel, IDDeviceFarm when IsFarmLevel, IDFarmOpenfieldCropParcel when IsParcelLevel.
        public required int ScopeId { get; init; }
        public bool IsUnitLevel { get; init; }
        public bool IsFarmLevel { get; init; }
        /// Open-Field's equivalent of the (implicit) Zone level - a Parcel has the same "one controller, Active/Stop tracked" shape as a Zone.
        public bool IsParcelLevel { get; init; }
        public required RelayFunction RelayFunction { get; init; }
        public required string Label { get; init; }
        public int? MaxRunSeconds { get; init; }
        public required IList<SensorMetric> AllowedTargetMetrics { get; init; }
        public DeviceManualOverride? Active { get; init; }
        public string DisplayTimeZone { get; init; } = "UTC";
    }
}
