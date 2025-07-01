using Microsoft.EntityFrameworkCore;
using MyJournalApp.Data.Models;
using MyJournalApp.Data;
using MyJournalApp.Interface;

namespace MyJournalApp.Repository
{
    public class GradeRepository : Repository<Grade>, IGradeRepository
    {
        public GradeRepository(JournalDbContext context) : base(context) { }

        public async Task<List<Grade>> GetGradesByStudentAsync(Guid studentId)
        {
            return await _dbSet
                .Where(g => g.StudentId == studentId)
                .ToListAsync();
        }

        public async Task<List<Grade>> GetGradesByStudentAndSemesterAsync(Guid studentId, int semester)
        {
            var (start, end) = semester switch
            {
                1 => (new DateTime(DateTime.Now.Year, 1, 1), new DateTime(DateTime.Now.Year, 6, 30)),
                2 => (new DateTime(DateTime.Now.Year, 7, 1), new DateTime(DateTime.Now.Year, 12, 31)),
                _ => (DateTime.MinValue, DateTime.MaxValue)
            };

            return await _dbSet
                .Where(g => g.StudentId == studentId && g.Date >= start && g.Date <= end)
                .ToListAsync();
        }
        public async Task<List<Grade>> GetGradesForGroupCourseAsync(Guid groupId, Guid courseId)
        {
            var studentIds = await _context.Students
                .Where(s => s.GroupId == groupId)
                .Select(s => s.Id)
                .ToListAsync();

            return await _dbSet
                .Where(g => studentIds.Contains(g.StudentId) && g.CourseId == courseId)
                .ToListAsync();
        }

        public async Task<Dictionary<string, double>> GetAverageGradesByCourseAsync(Guid studentId)
        {
            var grades = await _dbSet
                .Where(g => g.StudentId == studentId)
                .ToListAsync();

            var courseTitles = await _context.Courses
                .ToDictionaryAsync(c => c.Id, c => c.Title);

            return grades
                .GroupBy(g => g.CourseId)
                .ToDictionary(
                    g => courseTitles.GetValueOrDefault(g.Key, "Unknown"),
                    g => g.Average(x => x.Value)
                );
        }
    }

}
