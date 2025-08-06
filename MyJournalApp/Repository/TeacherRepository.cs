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

    private string ToShortName(string fullName)
    {
        // Пример: "Коноваленко Анжеліка Владиславівна" → "Коноваленко А.В."
        if (string.IsNullOrWhiteSpace(fullName)) return string.Empty;

        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return fullName;

        var surname = parts[0];
        var initials = string.Join(".", parts.Skip(1).Select(p => p[0])) + ".";
        return $"{surname} {initials}";
    }

}
