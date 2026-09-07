using api.Models;

namespace api.ViewModels
{
    /// Bound from DeviceFarmUnitController's RuleAdd/UnitRuleAdd/GlobalRuleAdd forms (roadmap #396(4)) - RootConditionJson is the whole ConditionNode tree, built client-side by wwwroot/js/rule-builder.js and serialized into one hidden field on submit, deserialized server-side with ConditionConfigJson.Options. A JS-free fallback isn't attempted (the old per-slot form couldn't express real nesting anyway).
    public class RuleFormInput
    {
        public ActionType ActionType { get; set; } = api.Models.ActionType.Relay;
        public RelayFunction? RelayFunction { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public bool IsSafetyRule { get; set; }
        public string? NotificationSubject { get; set; }
        public string? NotificationBody { get; set; }
        public string? RootConditionJson { get; set; }
    }
}
