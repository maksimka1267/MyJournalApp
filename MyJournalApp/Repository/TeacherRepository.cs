using Microsoft.EntityFrameworkCore;
using MyJournalApp.Data;
using MyJournalApp.Data.Models;

public class TeacherRepository : Repository<Teacher>, ITeacherRepository
{
    public TeacherRepository(JournalDbContext context) : base(context) { }

    public async Task<Teacher?> GetByGroupIdAsync(Guid groupId)
    {
        return await _context.Teachers
            .FirstOrDefaultAsync(t => t.GroupIds != null && t.GroupIds.Contains(groupId));
    }
    public async Task<bool> IsTeacherAsync(Guid userId)
    {
        // Проверяем Users: есть ли такой пользователь с ролью Teacher
        return await _context.Users
            .AnyAsync(u => u.Id == userId && u.Role == "Teacher");
    }

    public async Task<List<User>> GetAllTeachersAsync()
    {
        return await _context.Users
            .Where(u => u.Role == "Teacher")
            .ToListAsync();
    }
    public async Task<List<Teacher>> GetAllTeachersWithAdminAsync()
    {
        return await _context.Teachers
            .Where(u => u.IsAdmin == true)
            .ToListAsync();
    }
    public async Task<Guid?> GetTeacherIdByFullNameAsync(string shortName)
    {
        if (string.IsNullOrWhiteSpace(shortName))
            return null;

        // Пример: "Коноваленко А.В."
        shortName = shortName.Trim().ToLower();

        var teachers = await _context.Users
            .Where(u => u.Role == "Teacher")
            .ToListAsync();

        foreach (var teacher in teachers)
        {
            var compact = ToShortName(teacher.FullName);
            if (compact.ToLower() == shortName)
                return teacher.Id;
        }

        return null;
    }
    public async Task<User?> GetTeacherModelByFullNameAsync(string shortName)
    {
        if (string.IsNullOrWhiteSpace(shortName))
            return null;

        // Пример: "Коноваленко А.В."
        shortName = shortName.Trim().ToLower();

        var teachers = await _context.Users
            .Where(u => u.Role == "Teacher")
            .ToListAsync();

        foreach (var teacher in teachers)
        {
            var compact = ToShortName(teacher.FullName);
            if (compact.ToLower() == shortName)
                return teacher;
        }

        return null;
    }
    public async Task<Guid?> GetTeacherIdByUserIdAsync(Guid userId)
    {
        // Предполагаем: Teacher.Id == User.Id для преподавателей
        var exists = await _context.Teachers.AnyAsync(t => t.Id == userId);
        return exists ? userId : (Guid?)null;
    }

    public string ToShortName(string fullName)
    {
        // Пример: "Коноваленко Анжеліка Владиславівна" → "Коноваленко А.В."
        if (string.IsNullOrWhiteSpace(fullName)) return string.Empty;

        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return fullName;

        var surname = parts[0];
        var initials = string.Join(".", parts.Skip(1).Select(p => p[0])) + ".";
        return $"{surname} {initials}";
    }

    public async Task<string?> GetFullNameByIdAsync(Guid teacherId)
    {
        return await _context.Users
            .Where(u => u.Id == teacherId && u.Role == "Teacher")
            .Select(u => u.FullName)
            .FirstOrDefaultAsync();
    }
}
