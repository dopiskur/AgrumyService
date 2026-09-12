using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;

namespace Agrumy.Api.Tests.TestSupport
{
    internal sealed partial class AllFacetsRepository
    {
        public Task<IList<FarmGroup>> FarmGroupsGetAsync(int? tenantID) => farmGroupRepository.FarmGroupsGetAsync(tenantID);
        public Task<FarmGroup?> FarmGroupGetByIdAsync(int idFarmGroup) => farmGroupRepository.FarmGroupGetByIdAsync(idFarmGroup);
        public Task<FarmGroup> FarmGroupCreateAsync(string? name, int? tenantID) => farmGroupRepository.FarmGroupCreateAsync(name, tenantID);
        public Task FarmGroupDeleteAsync(int idFarmGroup) => farmGroupRepository.FarmGroupDeleteAsync(idFarmGroup);
        public Task FarmAssignToGroupAsync(int idFarm, int? idFarmGroup) => farmGroupRepository.FarmAssignToGroupAsync(idFarm, idFarmGroup);
    }
}
