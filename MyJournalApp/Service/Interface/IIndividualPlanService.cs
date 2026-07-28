using MyJournalApp.Dtos.IndividualPlan;
using MyJournalApp.Result;

namespace MyJournalApp.Service.Interface
{
    public interface IIndividualPlanService
    {
        Task<ServiceResult<IndividualPlanFileDto>> DownloadForMeAsync(
            Guid currentUserId,
            int? semester);

        Task<ServiceResult<IndividualPlanFileDto>> DownloadForStudentAsync(
            Guid currentUserId,
            DownloadIndividualPlanRequestDto dto);
    }
}
