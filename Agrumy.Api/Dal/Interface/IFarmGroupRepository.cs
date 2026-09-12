using Agrumy.Shared.Models;

namespace Agrumy.Api.Dal.Interface
{
    /// FarmGroup CRUD and farm membership - a cross-cutting grouping ABOVE Farm, spanning FarmType, kept deliberately separate from IDeviceFarmUnitRepository (same "avoid cross-controller coupling" reasoning as this codebase's other narrow facets).
    public interface IFarmGroupRepository
    {
        Task<IList<FarmGroup>> FarmGroupsGetAsync(int? tenantID);

        Task<FarmGroup?> FarmGroupGetByIdAsync(int idFarmGroup);

        Task<FarmGroup> FarmGroupCreateAsync(string? name, int? tenantID);

        /// Soft-deletes the group AND detaches every member farm's FarmGroupID back to null in the same call - a farm's own row/children are never touched, only its group membership.
        Task FarmGroupDeleteAsync(int idFarmGroup);

        /// Sets (or clears, when idFarmGroup is null) one farm's group membership - at most one group per farm, so this replaces any prior membership rather than adding to it.
        Task FarmAssignToGroupAsync(int idFarm, int? idFarmGroup);
    }
}
