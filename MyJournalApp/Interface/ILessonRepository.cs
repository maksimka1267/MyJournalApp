public interface ILessonRepository : IRepository<Lesson>
{
    Task<IEnumerable<Lesson>> GetLessonsByGroupIdAsync(Guid groupId);
    Task<IEnumerable<Lesson>> GetLessonsByDateAsync(Guid groupId, DateTime date);
}
