using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

public interface IJournalGenerationService
{
    Task<GenerationResult> GenerateJournalsFromScheduleAsync();
}

public class JournalGenerationService : IJournalGenerationService
{
    private readonly IGroupRepository _groupRepo;
    private readonly ILessonRepository _lessonRepo;
    private readonly IJournalEntryRepository _journalRepo;

    public JournalGenerationService(
        IGroupRepository groupRepo,
        ILessonRepository lessonRepo,
        IJournalEntryRepository journalRepo)
    {
        _groupRepo = groupRepo;
        _lessonRepo = lessonRepo;
        _journalRepo = journalRepo;
    }

    public async Task<GenerationResult> GenerateJournalsFromScheduleAsync()
    {
        // 1) Групи з розкладом
        var groupsWithLessons = await _groupRepo.GetGroupsWithLessonsAsync();
        if (!groupsWithLessons.Any())
            return new GenerationResult { Message = "Немає груп із розкладом для обробки." };

        var groupsDict = groupsWithLessons.ToDictionary(g => g.Id);
        var groupIds = groupsDict.Keys.ToList();

        // 2) Уроки цих груп
        var allLessons = await _lessonRepo.GetAllAsync();
        var relevantLessons = allLessons.Where(l => groupIds.Contains(l.GroupId));

        // 3) Групуємо лише за (нормалізованаНазва, GroupId)
        var lessonGroups = relevantLessons
            .Select(l => new { Lesson = l, SubjectNorm = NormalizeSubjectName(l.Name) })
            .GroupBy(x => new { x.SubjectNorm, x.Lesson.GroupId });

        // 4) Витягаємо ІСНУЮЧІ назви журналів (Name) для цих груп
        //   ⚠️ Очікується метод, що повертає: List<(Guid GroupId, string Name)>
        var existing = await _journalRepo.GetJournalNamesWithGroupAsync(groupIds);

        // Нормалізовані ключі існуючих журналів: (GroupId, SubjectNorm, SemesterText)
        var existingKeys = existing
            .Select(e =>
            {
                var name = e.Name ?? "";
                var subjPart = name;
                var idx = name.IndexOf(" - ", StringComparison.Ordinal);
                if (idx >= 0) subjPart = name[..idx];
                var subjNorm = NormalizeSubjectName(subjPart);
                var sem = ExtractSemesterSuffix(name); // "1 семестр 2025/2026" тощо
                return (e.GroupId, subjNorm, sem);
            })
            .ToHashSet();

        var journalsToCreate = new List<JournalEntry>();

        // 5) Створюємо журнали
        foreach (var gr in lessonGroups)
        {
            var key = gr.Key;
            var firstLesson = gr.First().Lesson;
            var group = groupsDict[key.GroupId];

            var semester = GetSemesterForDate(firstLesson.StartTime);  // "1 семестр YYYY/YYYY+1"
            var candidateKey = (key.GroupId, key.SubjectNorm, semester);

            if (existingKeys.Contains(candidateKey))
                continue;

            // зібрати всіх викладачів (TeacherId + SecondTeacherId)
            var teacherIds = gr.Select(x => x.Lesson.TeacherId)
                               .Where(id => id != Guid.Empty)
                               .Concat(gr.Select(x => x.Lesson.SecondTeacherId)
                                         .Where(id => id.HasValue)
                                         .Select(id => id!.Value))
                               .Distinct()
                               .ToList();

            var journalName = $"{key.SubjectNorm} - {group.Name} ({semester})";

            journalsToCreate.Add(new JournalEntry
            {
                Id = Guid.NewGuid(),
                Name = journalName,                         // ← саме Name показується в UI
                Subject = "Згенеровано автоматично",
                GroupId = key.GroupId,
                TeacherId = teacherIds,                    // один журнал — усі викладачі
                MaxValue = 12,
                Date = DateTime.UtcNow,
                Comment = "Згенеровано автоматично"
            });
        }

        // 6) Збереження
        if (journalsToCreate.Any())
        {
            await _journalRepo.AddRangeAsync(journalsToCreate);
            await _journalRepo.SaveChangesAsync();
        }

        return new GenerationResult
        {
            Success = true,
            CreatedCount = journalsToCreate.Count,
            Message = $"Операцію завершено. Створено нових журналів: {journalsToCreate.Count}."
        };
    }

    // —— helpers ————————————————————————————————————————————————————————

    // Нова нормалізація: зрізає підгрупний хвіст у останніх 1–2 словах
    private static string NormalizeSubjectName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        // 1) прибираємо зайві пробіли
        var s = raw.Replace("  ", " ").Trim();

        // 2) розбиваємо на токени та чистимо пунктуацію по краях
        static string Clean(string t)
        {
            if (string.IsNullOrWhiteSpace(t)) return "";
            t = t.Trim().Trim(',', '.', ';', ':', '-', '–', '—', '(', ')', '[', ']', '{', '}', '«', '»', '\'', '"');
            return t;
        }

        var tokens = s.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                      .Select(Clean)
                      .Where(t => t.Length > 0)
                      .ToList();

        if (tokens.Count == 0) return "";

        // 3) допоміжні перевірки
        static bool IsRoman(string t)
        {
            var tt = t.Trim('.').ToLowerInvariant();
            return tt is "i" or "ii" or "і" or "іі" or "1" or "2";
        }

        static bool IsSubgroupWord(string t)
        {
            var tt = t.Trim('.').ToLowerInvariant();
            return tt is "підг" or "підгр" or "гр" or "група" or "р";
        }

        // 4) зрізаємо хвіст (останнє/останні два слова)
        void TrimSubgroupTail()
        {
            if (tokens.Count == 0) return;

            string last = tokens[^1];
            string? prev = tokens.Count >= 2 ? tokens[^2] : null;

            bool lastIsRoman = IsRoman(last);
            bool lastIsSub = IsSubgroupWord(last);
            bool prevIsRoman = prev != null && IsRoman(prev);
            bool prevIsSub = prev != null && IsSubgroupWord(prev);

            // двослівні хвости
            if (prev != null && ((prevIsRoman && lastIsSub) || (prevIsSub && lastIsRoman)))
            {
                tokens.RemoveAt(tokens.Count - 1);
                tokens.RemoveAt(tokens.Count - 1);
                return;
            }

            // однословні хвости
            if (lastIsSub || lastIsRoman)
            {
                tokens.RemoveAt(tokens.Count - 1);
            }
        }

        // один/два проходи (на випадок «… І р.»)
        TrimSubgroupTail();
        TrimSubgroupTail();

        // 5) зібрати назад
        var result = string.Join(' ', tokens).Trim();
        while (result.Contains("  ")) result = result.Replace("  ", " ");
        result = result.Trim(' ', ',', '.', '-', '–', '—');
        return result;
    }

    private static string ExtractSemesterSuffix(string name)
    {
        // з "Предмет - Група (1 семестр 2025/2026)" -> "1 семестр 2025/2026"
        var start = name.LastIndexOf('(');
        var end = name.LastIndexOf(')');

        if (start >= 0 && end > start)
            return name.Substring(start + 1, end - start - 1).Trim();

        return "";
    }

    private string GetSemesterForDate(DateTime lessonDate)
    {
        int m = lessonDate.Month, y = lessonDate.Year;
        if (m >= 9 && m <= 12) return $"1 семестр {y}/{y + 1}";
        if (m >= 1 && m <= 6) return $"2 семестр {y - 1}/{y}";
        return "Міжсезоння";
    }
}

// Клас для результату генерації
public class GenerationResult
{
    public bool Success { get; set; }
    public int CreatedCount { get; set; }
    public string Message { get; set; }
}
