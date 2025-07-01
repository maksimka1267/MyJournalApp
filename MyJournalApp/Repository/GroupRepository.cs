using Microsoft.EntityFrameworkCore;
using MyJournalApp.Data;
using MyJournalApp.Data.Models;
using MyJournalApp.Interface;

namespace MyJournalApp.Repository
{
    public class GroupRepository : Repository<Group>, IGroupRepository
    {
        public GroupRepository(JournalDbContext context) : base(context) { }

        public async Task<Group?> GetGroupWithStudentsAsync(Guid groupId)
        {
            return await _dbSet
                .Include(g => g.Students)
                .FirstOrDefaultAsync(g => g.Id == groupId);
        }
    }

}
