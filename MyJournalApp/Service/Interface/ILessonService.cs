using MyJournalApp.Data.Dtos.Lesson;

namespace MyJournalApp.Service.Interface
{
    public interface ILessonService
    {
        Task<IEnumerable<Lesson>> GetAllAsync();

        Task<Lesson?> GetByIdAsync(Guid id);

        Task<IEnumerable<Lesson>> GetByGroupAsync(Guid groupId);

        Task<IEnumerable<Lesson>> GetByGroupAndDateAsync(Guid groupId, DateTime date);

        Task<Lesson> CreateAsync(CreateLessonRequest request);

        Task<bool> UpdateAsync(Guid id, Lesson lesson);

        Task<bool> DeleteAsync(Guid id);

        Task<List<string>> GetSubjectsByGroupAsync(Guid groupId);
    }
}
