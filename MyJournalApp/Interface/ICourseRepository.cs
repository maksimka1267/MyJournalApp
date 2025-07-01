using MyJournalApp.Data.Models;

namespace MyJournalApp.Interface
{
    public interface ICourseRepository : IRepository<Course>
    {
        Task<List<Course>> GetCoursesByTeacherAsync(Guid teacherId);
    }
}
