using MyJournalApp.Data.Dtos.Lesson;

namespace MyJournalApp.Service.Interface
{
    public interface ILessonBulkService
    {
        Task<BulkApplyResultDto> BulkApplyAsync(BulkApplyDto dto);
    }
}
