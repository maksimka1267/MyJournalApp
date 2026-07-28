using MyJournalApp.Interface;
using MyJournalApp.Service.Interface;

namespace MyJournalApp.Service
{
    public class ScheduleService : IScheduleService
    {
        private readonly IScheduleRepository _scheduleRepository;
        private readonly ILessonRepository _lessonRepository;

        public ScheduleService(
            IScheduleRepository scheduleRepository,
            ILessonRepository lessonRepository)
        {
            _scheduleRepository = scheduleRepository;
            _lessonRepository = lessonRepository;
        }

        public async Task<IEnumerable<Schedule>> GetAllAsync()
        {
            return await _scheduleRepository.GetAllAsync();
        }

        public async Task<Schedule?> GetByIdAsync(Guid id)
        {
            return await _scheduleRepository.GetByIdAsync(id);
        }

        public async Task<Schedule?> GetByGroupAndWeekAsync(Guid groupId, DateOnly weekStart)
        {
            return await _scheduleRepository.GetByGroupAndWeekAsync(groupId, weekStart);
        }

        public async Task<Schedule> CreateAsync(Schedule schedule)
        {
            await ValidateScheduleAsync(schedule);

            var existing = await _scheduleRepository
                .GetByGroupAndWeekAsync(schedule.GroupId, schedule.WeekStartDate);

            if (existing != null)
                throw new InvalidOperationException(
                    "Schedule already exists for this group and week.");

            await _scheduleRepository.AddAsync(schedule);
            await _scheduleRepository.SaveChangesAsync();

            return schedule;
        }

        public async Task<bool> UpdateAsync(Guid id, Schedule schedule)
        {
            var existing = await _scheduleRepository.GetByIdAsync(id);

            if (existing == null)
                return false;

            await ValidateScheduleAsync(schedule);

            existing.GroupId = schedule.GroupId;
            existing.WeekStartDate = schedule.WeekStartDate;
            existing.Lessons = schedule.Lessons;

            await _scheduleRepository.Update(existing);
            await _scheduleRepository.SaveChangesAsync();

            return true;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var existing = await _scheduleRepository.GetByIdAsync(id);

            if (existing == null)
                return false;

            await _scheduleRepository.Delete(existing);
            await _scheduleRepository.SaveChangesAsync();

            return true;
        }

        private async Task ValidateScheduleAsync(Schedule schedule)
        {
            foreach (var lessonId in schedule.Lessons)
            {
                var lesson = await _lessonRepository.GetByIdAsync(lessonId);

                if (lesson == null)
                    throw new ArgumentException(
                        $"Lesson with ID {lessonId} does not exist.");
            }
        }
    }
}