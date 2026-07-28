using MyJournalApp.Data.Dtos.StudentGrades;

namespace MyJournalApp.Service.Interface
{
    public interface IStudentGradesReportService
    {
        Task<StudentGradesReportDto> BuildReportAsync(
            Guid studentId,
            DateTime start,
            DateTime end);
    }

}
