using MyJournalApp.Data.Dtos.Lesson;

namespace MyJournalApp.Service.Interface
{
    public interface ILessonImportService
    {
        Task<ImportResultDto> ImportAsync(ImportLessonsDto dto);
    }
}
