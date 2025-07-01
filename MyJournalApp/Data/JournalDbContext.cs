using Microsoft.EntityFrameworkCore;
using MyJournalApp.Data.Models;

namespace MyJournalApp.Data
{
    public class JournalDbContext : DbContext
    {
        public JournalDbContext(DbContextOptions<JournalDbContext> options) : base(options) { }

        public DbSet<Client> Clients => Set<Client>();
        public DbSet<Student> Students => Set<Student>();
        public DbSet<Teacher> Teachers => Set<Teacher>();
        public DbSet<Admin> Admins => Set<Admin>();

        public DbSet<Group> Groups => Set<Group>();
        public DbSet<Course> Courses => Set<Course>();
        public DbSet<Grade> Grades => Set<Grade>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // 👇 Налаштування TPT (Table-Per-Type)
            modelBuilder.Entity<Client>().ToTable("Clients");
            modelBuilder.Entity<Student>().ToTable("Students");
            modelBuilder.Entity<Teacher>().ToTable("Teachers");
            modelBuilder.Entity<Admin>().ToTable("Admins");

            base.OnModelCreating(modelBuilder);
        }
    }
}
