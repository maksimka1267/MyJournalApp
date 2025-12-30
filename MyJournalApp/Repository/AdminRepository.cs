using MyJournalApp.Data;
using MyJournalApp.Data.Models;
using MyJournalApp.Interface;

namespace MyJournalApp.Repository
{
    public class AdminRepository:Repository<Admin>, IAdminRepository
    {
        public AdminRepository(JournalDbContext context) : base(context) { }
    }
}
