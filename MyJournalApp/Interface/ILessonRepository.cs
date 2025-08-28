public interface ILessonRepository : IRepository<Lesson>
{
    Task<IEnumerable<Lesson>> GetLessonsByGroupIdAsync(Guid groupId);
    Task<IEnumerable<Lesson>> GetLessonsByDateAsync(Guid groupId, DateTime date);
    Task DeleteLessonsAsync(IEnumerable<Lesson> lessons);
    Task<List<Lesson>> GetByTeacherAsync(
        Guid teacherId,
        DateTime from,
        DateTime to,
        Guid? groupId,
        string? subject);

    Task AddRangeAsync(IEnumerable<Lesson> lessons);
}
