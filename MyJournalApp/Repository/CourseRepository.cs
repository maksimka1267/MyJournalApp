using MyJournalApp.Data.Models;
using MyJournalApp.Data;
using MyJournalApp.Interface;
using Microsoft.EntityFrameworkCore;

namespace MyJournalApp.Repository
{
    public class CourseRepository : Repository<Course>, ICourseRepository
    {
        public CourseRepository(JournalDbContext context) : base(context) { }

        public async Task<List<Course>> GetCoursesByTeacherAsync(Guid teacherId)
        {
            // Найти все курсы, на которые есть оценки от этого преподавателя
            var courseIds = await _context.Grades
                .Where(g => g.TeacherId == teacherId)
                .Select(g => g.CourseId)
                .Distinct()
                .ToListAsync();

            return await _dbSet
                .Where(c => courseIds.Contains(c.Id))
                .ToListAsync();
        }

    }

}
