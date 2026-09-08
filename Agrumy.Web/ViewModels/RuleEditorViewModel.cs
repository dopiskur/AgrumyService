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
        Experiment,
    }

    /// Drives _RuleEditor.cshtml, shared across the Zone page, Unit "Rules" tab, the Farm "Rules" tab, the tenant-wide Global Rules page, a Simulation session's own Details page, and an Experiment's own Details page - the six scopes differ only in which API routes/hidden field they post to.
    public class RuleEditorViewModel
    {
        public required RuleScope Scope { get; init; }

        /// IDDeviceFarmUnitZone for Zone scope, IDDeviceFarmUnit for Unit scope, IDDeviceFarm for Farm scope, IDSimulationSession for Simulation scope, IDExperiment for Experiment scope, null for Global (implied by the caller's tenant).
        public int? ScopeId { get; init; }

        public IList<DeviceFarmUnitZoneRule> Rules { get; init; } = [];

        public string AddActionName => Scope switch
        {
            RuleScope.Zone => "RuleAdd",
            RuleScope.Unit => "UnitRuleAdd",
            RuleScope.Farm => "DeviceFarmRuleAdd",
            RuleScope.Simulation => "SessionRuleAdd",
            RuleScope.Experiment => "ExperimentRuleAdd",
            _ => "GlobalRuleAdd",
        };

        public string DeleteActionName => Scope switch
        {
            RuleScope.Zone => "RuleDelete",
            RuleScope.Unit => "UnitRuleDelete",
            RuleScope.Farm => "DeviceFarmRuleDelete",
            RuleScope.Simulation => "SessionRuleDelete",
            RuleScope.Experiment => "ExperimentRuleDelete",
            _ => "GlobalRuleDelete",
        };

        /// "" for Global, where there's no scope id to carry - RuleAdd's own scope check (server-side) resolves it from the caller's tenant instead.
        public string ScopeHiddenFieldName => Scope switch
        {
            RuleScope.Zone => "idDeviceFarmUnitZone",
            RuleScope.Unit => "idDeviceFarmUnit",
            RuleScope.Farm => "idDeviceFarm",
            RuleScope.Simulation => "idSimulationSession",
            RuleScope.Experiment => "idExperiment",
            _ => "",
        };

        /// Which page RuleAdd/RuleDelete redirect back to, so this same partial works unmodified across all three host pages.
        public required string RedirectActionName { get; init; }
    }

    /// One row of GlobalRules' flat, searchable overview across every scope in the tenant - a triage list, not a detail view, so it carries only what the table shows plus a link to the rule's own scope page for editing.
    public class RuleOverviewRow
    {
        public required DeviceFarmUnitZoneRule Rule { get; init; }
        public required string ScopeLabel { get; init; }
        public required string DetailUrl { get; init; }
    }

    /// GlobalRules page model - the tenant-wide overview table (AllRules) plus the existing Global-scope rule editor (Editor), which still lives on this same page for actually adding/removing Global-scope rules.
    public class GlobalRulesPageViewModel
    {
        public required RuleEditorViewModel Editor { get; init; }
        public required IList<RuleOverviewRow> AllRules { get; init; }
    }
}
