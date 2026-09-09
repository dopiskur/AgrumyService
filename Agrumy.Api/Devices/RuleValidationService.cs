using System.ComponentModel.DataAnnotations;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;

namespace Agrumy.Api.Devices
{
    /// Shape+bound validation for a DeviceFarmUnitZoneRule tree, shared by every controller that writes rules at any scope (DeviceFarmUnitApiController's Zone/Unit/Farm/Global routes, SimulationApiController's Simulation-scoped routes) - extracted so the two don't drift out of sync on what counts as a valid rule.
    public class RuleValidationService(IDeviceFarmUnitRepository deviceFarmUnitRepo)
    {
        // Kept as forwards to DeviceFarmUnitZoneRule's own copy (the actual source of truth now) so existing callers of RuleValidationService.HardMax* don't need to change.
        public const int HardMaxNodesPerRule = DeviceFarmUnitZoneRule.HardMaxNodesPerRule;
        public const int HardMaxChildrenPerGroup = DeviceFarmUnitZoneRule.HardMaxChildrenPerGroup;

        /// Shape+bound check for the whole rule: everything DeviceFarmUnitZoneRule.Validate() itself can check
        /// (ActionType/RelayFunction/Name consistency, tree-size bounds, per-node shape recursively) - that same
        /// method also runs automatically wherever the DTO is model-bound, so a rule posted through any
        /// [ApiController] action already gets these checks even before reaching here. This method's own,
        /// non-duplicated job is the one check that needs a DB lookup and so can't live on the DTO itself:
        /// RuleTriggered's referenced-rule existence/tenant/action-type cross-reference.
        public async Task<string?> ShapeErrorAsync(DeviceFarmUnitZoneRule rule)
        {
            var results = new List<ValidationResult>();
            if (!Validator.TryValidateObject(rule, new ValidationContext(rule), results, validateAllProperties: true))
            {
                return results[0].ErrorMessage;
            }
            return rule.Root == null ? null : await RuleTriggeredReferenceErrorAsync(rule.Root, rule);
        }

        /// The one piece of rule-tree validation that needs a DB round trip, so DeviceFarmUnitZoneRule.Validate() (a Shared-project DTO with no DB access) deliberately leaves it to this method.
        private async Task<string?> RuleTriggeredReferenceErrorAsync(ConditionNode node, DeviceFarmUnitZoneRule rule)
        {
            if (node.Type == NodeType.RuleTriggered)
            {
                DeviceFarmUnitZoneRule? referenced = node.ReferencedRuleId is int refId ? await deviceFarmUnitRepo.RuleGetByIdAsync(refId) : null;
                if (referenced == null || referenced.TenantID != rule.TenantID || referenced.ActionType != ActionType.Notification)
                {
                    return "\"another rule fired\" must reference a rule that exists, belongs to the same tenant, and is a Notification-action rule.";
                }
            }
            foreach (ConditionNode child in node.Children)
            {
                if (await RuleTriggeredReferenceErrorAsync(child, rule) is string childError)
                {
                    return childError;
                }
            }
            return null;
        }
    }
}
