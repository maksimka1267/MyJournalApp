using Microsoft.EntityFrameworkCore;
using MyJournalApp.Data;
using MyJournalApp.Interface;

namespace MyJournalApp.Repository
{
    public class StudentRepository : Repository<Student>, IStudentRepository
    {
        public StudentRepository(JournalDbContext context) : base(context) { }

        public async Task<IEnumerable<Student>> GetByGroupIdAsync(Guid groupId)
        {
            return await _context.Students
                .Where(s => s.GroupId == groupId)
                .ToListAsync();
        }
        public async Task<List<User>> GetAllStudentsAsync()
        {
            return await _context.Users
                .Where(u => u.Role == "Student")
                .ToListAsync();
        }
        public async Task<List<User>> GetUsersByGroupIdAsync(Guid groupId)
        {
            // Этот запрос объединяет таблицы Students и Users по их ID,
            // фильтрует студентов по groupId и возвращает полные модели User.
            return await _context.Students
                .Where(student => student.GroupId == groupId)
                .Join(_context.Users,
                      student => student.Id, // Ключ из таблицы Students
                      user => user.Id,       // Ключ из таблицы Users
                      (student, user) => user) // В результате выбираем объект User
                .ToListAsync();
        }

    }

}
