using Microsoft.EntityFrameworkCore;
using MyJournalApp.Data.Models;

namespace MyJournalApp.Data
{
    public class JournalDbContext : DbContext
    {
        public JournalDbContext(DbContextOptions<JournalDbContext> options) : base(options) { }

        public DbSet<Student> Students => Set<Student>();
        public DbSet<Group> Groups => Set<Group>();
        public DbSet<Teacher> Teachers => Set<Teacher>();
        public DbSet<Course> Courses => Set<Course>();
        public DbSet<Grade> Grades => Set<Grade>();
    }

}
