using MyJournalApp.Data.Dtos.GroupFiles;
using MyJournalApp.Result;

namespace MyJournalApp.Service.Interface
{
    public interface IGroupFilesService
    {
        Task<ServiceResult<GroupFilesStatusDto>> GetStatusAsync(Guid groupId);

        Task<IEnumerable<GroupFilesStatusDto>> GetStatusBatchAsync(IEnumerable<Guid> groupIds);

        Task<ServiceResult<UploadGroupFileResultDto>> UploadAsync(UploadGroupFileDto dto);

        Task<ServiceResult<FileDownloadDto>> DownloadAsync(Guid groupId, int semester);

        Task<IServiceResult> DeleteAsync(Guid groupId, int semester);
    }
}
