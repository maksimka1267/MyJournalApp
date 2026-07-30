using MyJournalApp.Data.Dtos.Lesson;
using MyJournalApp.Service.Interface;

namespace MyJournalApp.Service
{
    public class LessonService : ILessonService
    {
        private readonly ILessonRepository _lessonRepository;

        public LessonService(ILessonRepository lessonRepository)
        {
            _lessonRepository = lessonRepository;
        }
        public async Task<IEnumerable<Lesson>> GetAllAsync()
        {
            return await _lessonRepository.GetAllAsync();
        }
        public async Task<Lesson?> GetByIdAsync(Guid id)
        {
            return await _lessonRepository.GetByIdAsync(id);
        }
        public async Task<IEnumerable<Lesson>> GetByGroupAsync(Guid groupId)
        {
            return await _lessonRepository.GetLessonsByGroupIdAsync(groupId);
        }
        public async Task<IEnumerable<Lesson>> GetByGroupAndDateAsync(Guid groupId, DateTime date)
        {
            return await _lessonRepository.GetLessonsByDateAsync(groupId, date);
        }
        public async Task<Lesson> CreateAsync(CreateLessonRequest req)
        {
            if (req.GroupId == Guid.Empty)
                throw new ArgumentException("Потрібен GroupId.");

            if (req.TeacherId == Guid.Empty)
                throw new ArgumentException("Потрібен TeacherId.");

            if (req.StartTime == default)
                throw new ArgumentException("Потрібна дата початку.");

            var secondTeacherId =
                (req.SecondTeacherId.HasValue && req.SecondTeacherId.Value != Guid.Empty)
                    ? req.SecondTeacherId
                    : null;

            var baseLesson = new Lesson
            {
                Id = req.Id == Guid.Empty ? Guid.NewGuid() : req.Id,
                GroupId = req.GroupId,
                TeacherId = req.TeacherId,
                SecondTeacherId = secondTeacherId,
                Name = req.Name ?? "",
                Topic = req.Topic ?? "",
                Homework = req.Homework ?? "",
                StartTime = req.StartTime,
                Clocks = req.Clocks
            };

            if (!req.RepeatWeekly ||
                !req.EndDate.HasValue ||
                req.EndDate.Value.Date < req.StartTime.Date)
            {
                await _lessonRepository.AddAsync(baseLesson);
                await _lessonRepository.SaveChangesAsync();

                return baseLesson;
            }

            if (req.ForNumerator != 1 && req.ForDenominator != 1)
                throw new ArgumentException("Оберіть чисельник або знаменник.");

            var lessons = BuildLessonSeries(req);

            if (lessons.Count == 0)
                throw new ArgumentException("Немає дат для вибраної парності.");

            await _lessonRepository.AddRangeAsync(lessons);
            await _lessonRepository.SaveChangesAsync();

            return lessons.First();
        }
        public async Task<bool> UpdateAsync(Guid id, Lesson updated)
        {
            var existing = await _lessonRepository.GetByIdAsync(id);

            if (existing == null)
                return false;

            if (!string.IsNullOrWhiteSpace(updated.Name))
                existing.Name = updated.Name;

            if (updated.GroupId != Guid.Empty &&
                updated.GroupId != existing.GroupId)
                existing.GroupId = updated.GroupId;

            if (updated.TeacherId != Guid.Empty)
                existing.TeacherId = updated.TeacherId;

            if (updated.SecondTeacherId.HasValue)
                existing.SecondTeacherId =
                    updated.SecondTeacherId == Guid.Empty
                        ? null
                        : updated.SecondTeacherId;

            if (updated.Topic != null)
                existing.Topic = updated.Topic;

            if (updated.Homework != null)
                existing.Homework = updated.Homework;

            if (updated.Clocks.HasValue)
                existing.Clocks = updated.Clocks;

            if (updated.Number.HasValue)
                existing.Number = updated.Number;

            if (updated.StartTime != default)
                existing.StartTime = updated.StartTime;

            await _lessonRepository.Update(existing);
            await _lessonRepository.SaveChangesAsync();

            return true;
        }
        public async Task<bool> DeleteAsync(Guid id)
        {
            var lesson = await _lessonRepository.GetByIdAsync(id);

            if (lesson == null)
                return false;

            await _lessonRepository.Delete(lesson);
            await _lessonRepository.SaveChangesAsync();

            return true;
        }
        public async Task<List<string>> GetSubjectsByGroupAsync(Guid groupId)
        {
            var lessons = await _lessonRepository.GetLessonsByGroupIdAsync(groupId);

            return lessons
                .Select(x => x.Name)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .OrderBy(x => x)
                .ToList();
        }
        private List<Lesson> BuildLessonSeries(CreateLessonRequest request)
        {
            var secondTeacherId =
                request.SecondTeacherId.HasValue && request.SecondTeacherId != Guid.Empty
                    ? request.SecondTeacherId
                    : null;

            var lessons = new List<Lesson>();

            var startDate = request.StartTime.Date;
            var endDate = request.EndDate!.Value.Date;

            var timeOfDay = request.StartTime.TimeOfDay;
            var dayOfWeek = request.StartTime.DayOfWeek;

            var first = startDate;

            while (first.DayOfWeek != dayOfWeek)
                first = first.AddDays(1);

            for (var d = first; d <= endDate; d = d.AddDays(7))
            {
                var isNumerator = IsNumeratorWeek(d);

                if (isNumerator && request.ForNumerator != 1)
                    continue;

                if (!isNumerator && request.ForDenominator != 1)
                    continue;

                lessons.Add(new Lesson
                {
                    Id = Guid.NewGuid(),
                    GroupId = request.GroupId,
                    TeacherId = request.TeacherId,
                    SecondTeacherId = secondTeacherId,
                    Name = request.Name ?? string.Empty,
                    Topic = request.Topic ?? string.Empty,
                    Homework = request.Homework ?? string.Empty,
                    StartTime = d.Date + timeOfDay,
                    Clocks = request.Clocks
                });
            }

            return lessons;
        }
        private static DateTime GetNumeratorAnchorMonday(int year)
        {
            var firstDay = new DateTime(year, 1, 1);

            while (firstDay.DayOfWeek != DayOfWeek.Monday)
                firstDay = firstDay.AddDays(1);

            return firstDay.Date;
        }
        private static bool IsNumeratorWeek(DateTime date)
        {
            var anchorMonday = GetNumeratorAnchorMonday(date.Year);

            var currentMonday = date.Date.AddDays(
                -((7 + (int)date.DayOfWeek - (int)DayOfWeek.Monday) % 7));

            var weeks = (int)((currentMonday - anchorMonday).TotalDays / 7);

            return weeks % 2 == 0;
        }
    }
}
