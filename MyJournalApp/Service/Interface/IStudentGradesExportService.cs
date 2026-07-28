using MyJournalApp.Data.Dtos.StudentGrades;

namespace MyJournalApp.Service.Interface
{
    public interface IStudentGradesExportService
    {
        Task<StudentGradesExportDto> ExportAsync(
            Guid studentId,
            DateTime start,
            DateTime end);
    }

}
