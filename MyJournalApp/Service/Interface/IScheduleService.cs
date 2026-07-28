
namespace MyJournalApp.Service.Interface
{
    public interface IScheduleService
    {
        Task<IEnumerable<Schedule>> GetAllAsync();

        Task<Schedule?> GetByIdAsync(Guid id);

        Task<Schedule?> GetByGroupAndWeekAsync(Guid groupId, DateOnly weekStart);

        Task<Schedule> CreateAsync(Schedule schedule);

        Task<bool> UpdateAsync(Guid id, Schedule schedule);

        Task<bool> DeleteAsync(Guid id);
    }
}