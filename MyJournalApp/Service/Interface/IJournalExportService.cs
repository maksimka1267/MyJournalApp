using MyJournalApp.Data.Dtos.Journal;
using MyJournalApp.Data.Dtos.Lesson;
using MyJournalApp.Result;

namespace MyJournalApp.Service.Interface
{
    public interface IJournalExportService
    {
        Task<ServiceResult<JournalExportDto>> ExportAsync(Guid journalId);
        Task<ServiceResult<List<JournalExportItemDto>>> GetJournalsAsync(
        ExportSemesterRequestDto dto);
        Task<ServiceResult<JournalExportDto>> ExportSemesterAsync(
        ExportSemesterRequestDto dto);

    }
}
