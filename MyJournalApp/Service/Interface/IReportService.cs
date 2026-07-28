using MyJournalApp.Data.Dtos.Absence;

namespace MyJournalApp.Service.Interface
{
    public interface IReportService
    {
        Task<ReportFileDto> GenerateAbsenceReportAsync(AbsenceReportDto dto);
    }
}
