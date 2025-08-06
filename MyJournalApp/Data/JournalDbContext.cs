using Microsoft.EntityFrameworkCore;
using MyJournalApp.Data.Models;

namespace MyJournalApp.Data
{
    public class JournalDbContext : DbContext
    {
        public JournalDbContext(DbContextOptions<JournalDbContext> options) : base(options) { }

        public DbSet<Student> Students => Set<Student>();
        public DbSet<Admin> Admins => Set<Admin>();
        public DbSet<Grade> Grades => Set<Grade>();
        public DbSet<User> Users => Set<User>();
        public DbSet<Lesson> Lessons => Set<Lesson>();
        public DbSet<Group> Groups => Set<Group>();
        public DbSet<Teacher> Teachers => Set<Teacher>();
        public DbSet<Schedule> Schedules => Set<Schedule>();
        public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
        public DbSet<AcademicEvent> AcademicEvents => Set<AcademicEvent>();

    }

}
