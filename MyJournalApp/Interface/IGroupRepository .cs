using MyJournalApp.Data.Models;

namespace MyJournalApp.Interface
{
    public interface IGroupRepository : IRepository<Group>
    {
        Task<Group?> GetGroupWithStudentsAsync(Guid groupId);
    }

}
