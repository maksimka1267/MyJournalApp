using MyJournalApp.Data.Models;

namespace MyJournalApp.Interface
{
    public interface IGradeRepository : IRepository<Grade>
    {
        Task<List<Grade>> GetGradesByStudentAsync(Guid studentId);
        Task<List<Grade>> GetGradesByStudentAndSemesterAsync(Guid studentId, int semester);
        Task<List<Grade>> GetGradesForGroupCourseAsync(Guid groupId, Guid courseId);
        Task<Dictionary<string, double>> GetAverageGradesByCourseAsync(Guid studentId);
    }
}
