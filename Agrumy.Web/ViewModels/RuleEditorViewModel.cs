using Agrumy.Shared.Models;

namespace Agrumy.Web.ViewModels
{
    public enum RuleScope
    {
        Zone,
        Unit,
        Farm,
        Global,
        Simulation,
    }

    /// Drives _RuleEditor.cshtml, shared across the Zone page, Unit "Rules" tab, the Farm "Rules" tab, the tenant-wide Global Rules page, and a Simulation session's own Details page - the five scopes differ only in which API routes/hidden field they post to.
    public class RuleEditorViewModel
    {
        public required RuleScope Scope { get; init; }

        /// IDDeviceFarmUnitZone for Zone scope, IDDeviceFarmUnit for Unit scope, IDDeviceFarm for Farm scope, IDSimulationSession for Simulation scope, null for Global (implied by the caller's tenant).
        public int? ScopeId { get; init; }

        public IList<DeviceFarmUnitZoneRule> Rules { get; init; } = [];

        public string AddActionName => Scope switch
        {
            RuleScope.Zone => "RuleAdd",
            RuleScope.Unit => "UnitRuleAdd",
            RuleScope.Farm => "DeviceFarmRuleAdd",
            RuleScope.Simulation => "SessionRuleAdd",
            _ => "GlobalRuleAdd",
        };

        public string DeleteActionName => Scope switch
        {
            RuleScope.Zone => "RuleDelete",
            RuleScope.Unit => "UnitRuleDelete",
            RuleScope.Farm => "DeviceFarmRuleDelete",
            RuleScope.Simulation => "SessionRuleDelete",
            _ => "GlobalRuleDelete",
        };

        /// "" for Global, where there's no scope id to carry - RuleAdd's own scope check (server-side) resolves it from the caller's tenant instead.
        public string ScopeHiddenFieldName => Scope switch
        {
            RuleScope.Zone => "idDeviceFarmUnitZone",
            RuleScope.Unit => "idDeviceFarmUnit",
            RuleScope.Farm => "idDeviceFarm",
            RuleScope.Simulation => "idSimulationSession",
            _ => "",
        };

        /// Which page RuleAdd/RuleDelete redirect back to, so this same partial works unmodified across all three host pages.
        public required string RedirectActionName { get; init; }
    }
}
