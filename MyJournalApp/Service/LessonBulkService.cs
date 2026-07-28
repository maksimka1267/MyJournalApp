using MyJournalApp.Data.Dtos.Lesson;
using MyJournalApp.Interface;
using MyJournalApp.Service.Interface;

namespace MyJournalApp.Service
{
    public class LessonBulkService : ILessonBulkService
    {
        private readonly ILessonRepository _lessonRepository;

        public LessonBulkService(ILessonRepository lessonRepository)
        {
            _lessonRepository = lessonRepository;
        }

        public async Task<BulkApplyResultDto> BulkApplyAsync(BulkApplyDto dto)
        {
            Validate(dto);

            var baseline = await LoadBaselineAsync(dto);

            var changes = BuildChanges(dto, baseline);

            return await ApplyChangesAsync(dto, changes);
        }
        private sealed class SlotSignature
        {
            public TimeSpan Time { get; init; }
            public string Name { get; init; } = string.Empty;
            public Guid TeacherId { get; init; }
            public Guid? SecondTeacherId { get; init; }

            public static SlotSignature From(Lesson l) => new()
            {
                Time = l.StartTime.TimeOfDay,
                Name = l.Name ?? string.Empty,
                TeacherId = l.TeacherId,
                SecondTeacherId = NormalizeSecond(l.SecondTeacherId)
            };

            public bool Matches(Lesson l)
            {
                var lT2 = NormalizeSecond(l.SecondTeacherId);
                return l.StartTime.TimeOfDay == Time
                       && string.Equals(l.Name ?? "", Name, StringComparison.Ordinal)
                       && l.TeacherId == TeacherId
                       && lT2 == SecondTeacherId;
            }

            private static Guid? NormalizeSecond(Guid? g) =>
                g.HasValue && g.Value != Guid.Empty ? g : null;
        }

        private sealed class SlotChanges
        {
            public SlotSignature Signature { get; init; } = null!;
            public required BulkApplyLessonDto NewValues { get; init; }
            public bool Delete { get; init; }

            public bool SetName { get; set; }
            public bool SetTeacherId { get; set; }
            public bool SetSecondTeacherId { get; set; }
            public bool SetTopic { get; set; }
            public bool SetHomework { get; set; }
            public bool SetClocks { get; set; }

            public bool HasAny =>
                SetName || SetTeacherId || SetSecondTeacherId || SetTopic || SetHomework || SetClocks;
        }
        private static void Validate(BulkApplyDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            if (dto.Lessons == null || dto.Lessons.Count == 0)
                throw new ArgumentException("Порожній пакет уроків.");

            var start = dto.StartDate.Date;
            var end = dto.EndDate.Date;

            if (end < start)
                throw new ArgumentException("Кінцева дата раніше за початкову.");

            var groupIds = dto.Lessons
                .Select(x => x.GroupId)
                .Distinct()
                .ToList();

            if (groupIds.Count != 1)
                throw new ArgumentException("Усі уроки мають бути однієї групи.");
        }
        private async Task<Dictionary<Guid, Lesson>> LoadBaselineAsync(BulkApplyDto dto)
        {
            var groupId = dto.Lessons.First().GroupId;

            var baseline = await _lessonRepository.GetLessonsByDateAsync(
                groupId,
                dto.StartDate.Date);

            return baseline.ToDictionary(x => x.Id, x => x);
        }
        private List<SlotChanges> BuildChanges(
    BulkApplyDto dto,
    Dictionary<Guid, Lesson> baseline)
        {
            var changes = new List<SlotChanges>();

            foreach (var lesson in dto.Lessons)
            {
                if (!baseline.TryGetValue(lesson.Id, out var oldLesson))
                    continue;

                var change = new SlotChanges
                {
                    Signature = SlotSignature.From(oldLesson),
                    NewValues = lesson,
                    Delete = lesson.Delete
                };

                if (!change.Delete)
                {
                    if (!string.Equals(oldLesson.Name ?? string.Empty,
                                       lesson.Name ?? string.Empty,
                                       StringComparison.Ordinal))
                        change.SetName = true;

                    if (oldLesson.TeacherId != lesson.TeacherId)
                        change.SetTeacherId = true;

                    var oldSecond = SlotSignature.From(oldLesson).SecondTeacherId;

                    var newSecond =
                        lesson.SecondTeacherId.HasValue &&
                        lesson.SecondTeacherId != Guid.Empty
                            ? lesson.SecondTeacherId
                            : null;

                    if (oldSecond != newSecond)
                        change.SetSecondTeacherId = true;

                    if (!string.Equals(oldLesson.Topic ?? string.Empty,
                                       lesson.Topic ?? string.Empty,
                                       StringComparison.Ordinal))
                        change.SetTopic = true;

                    if (!string.Equals(oldLesson.Homework ?? string.Empty,
                                       lesson.Homework ?? string.Empty,
                                       StringComparison.Ordinal))
                        change.SetHomework = true;

                    if (oldLesson.Clocks != lesson.Clocks)
                        change.SetClocks = true;
                }

                if (change.Delete || change.HasAny)
                    changes.Add(change);
            }

            return changes;
        }
        private async Task<BulkApplyResultDto> ApplyChangesAsync(
    BulkApplyDto dto,
    List<SlotChanges> changes)
        {
            int updated = 0;
            int deleted = 0;

            var start = dto.StartDate.Date;
            var end = dto.EndDate.Date;
            var groupId = dto.Lessons.First().GroupId;

            for (var date = start; date <= end; date = date.AddDays(7))
            {
                var dayLessons = (await _lessonRepository
                    .GetLessonsByDateAsync(groupId, date))
                    ?.ToList();

                if (dayLessons == null || dayLessons.Count == 0)
                    continue;

                foreach (var change in changes)
                {
                    var target = dayLessons
                        .FirstOrDefault(x => change.Signature.Matches(x));

                    if (target == null)
                        continue;

                    if (change.Delete)
                    {
                        await _lessonRepository.Delete(target);
                        deleted++;
                        continue;
                    }

                    var newValues = change.NewValues!;

                    if (change.SetName)
                        target.Name = newValues.Name;

                    if (change.SetTeacherId)
                        target.TeacherId = newValues.TeacherId;

                    if (change.SetSecondTeacherId)
                    {
                        target.SecondTeacherId =
                            newValues.SecondTeacherId.HasValue &&
                            newValues.SecondTeacherId != Guid.Empty
                                ? newValues.SecondTeacherId
                                : null;
                    }

                    if (change.SetTopic)
                        target.Topic = newValues.Topic;

                    if (change.SetHomework)
                        target.Homework = newValues.Homework;

                    if (change.SetClocks)
                        target.Clocks = newValues.Clocks;

                    await _lessonRepository.Update(target);
                    updated++;
                }
            }

            await _lessonRepository.SaveChangesAsync();

            return new BulkApplyResultDto
            {
                Updated = updated,
                Deleted = deleted
            };
        }
    }

}