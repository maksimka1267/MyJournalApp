using MyJournalApp.Data.Dtos.Lesson;

namespace MyJournalApp.Service.Interface
{
    public interface ILessonExportService
    {
        Task<LessonExportDto> ExportAsync(ExportDto dto);
        Task<LessonExportDto> ExportSemesterAsync(ExportSemesterLessonsDto dto);
    }
}
