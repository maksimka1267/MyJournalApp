public interface IScheduleRepository : IRepository<Schedule>
{
    Task<Schedule?> GetByGroupAndWeekAsync(Guid groupId, DateOnly weekStart);
}
