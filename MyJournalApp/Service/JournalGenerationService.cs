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
        if (groupsWithLessons == null || !groupsWithLessons.Any())
            return new GenerationResult { Success = true, CreatedCount = 0, Message = "Немає груп із розкладом для обробки." };

        var groupsDict = groupsWithLessons.ToDictionary(g => g.Id);
        var groupIds = groupsDict.Keys.ToList();
        var groupIdSet = groupIds.ToHashSet();

        // 2) Уроки цих груп (краще було б: GetByGroupIdsAsync(groupIds) на рівні БД)
        var allLessons = await _lessonRepo.GetAllAsync();
        var relevantLessons = allLessons
            .Where(l => groupIdSet.Contains(l.GroupId))
            .ToList();

        if (relevantLessons.Count == 0)
            return new GenerationResult { Success = true, CreatedCount = 0, Message = "Немає уроків для обраних груп." };

        // 3) Рахуємо SubjectNorm + Semester ДЛЯ КОЖНОГО уроку і групуємо з урахуванням семестру
        var lessonGroups = relevantLessons
            .Select(l => new
            {
                Lesson = l,
                SubjectNorm = NormalizeSubjectName(l.Name),
                Semester = GetSemesterForDate(l.StartTime)
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.SubjectNorm))
            .Where(x => x.Semester != "Міжсезоння") // якщо треба генерувати і міжсезоння — прибери цю строку
            .GroupBy(x => new { x.Lesson.GroupId, x.SubjectNorm, x.Semester });

        // 4) Витягаємо існуючі журнали для цих груп
        // Очікується: List<(Guid GroupId, string Name)>
        var existing = await _journalRepo.GetJournalNamesWithGroupAsync(groupIds);

        // Ключі існуючих журналів: (GroupId, SubjectNorm, Semester)
        var existingKeys = existing
            .Select(e =>
            {
                var name = e.Name ?? "";
                var subjPart = name;
                var idx = name.IndexOf(" - ", StringComparison.Ordinal);
                if (idx >= 0) subjPart = name[..idx];

                var subjNorm = NormalizeSubjectName(subjPart);
                var sem = ExtractSemesterSuffix(name); // "1 семестр 2025/2026" тощо (або "")
                return (e.GroupId, subjNorm, sem);
            })
            .Where(k => !string.IsNullOrWhiteSpace(k.subjNorm))
            .ToHashSet();

        var journalsToCreate = new List<JournalEntry>();

        foreach (var gr in lessonGroups)
        {
            var groupId = gr.Key.GroupId;
            var subjectNorm = gr.Key.SubjectNorm;
            var semester = gr.Key.Semester;

            if (!groupsDict.TryGetValue(groupId, out var group))
                continue; // дані неузгоджені — пропускаємо без падіння

            var candidateKey = (groupId, subjectNorm, semester);
            if (existingKeys.Contains(candidateKey))
                continue;

            // Збираємо всіх викладачів предмету в цьому семестрі (TeacherId + SecondTeacherId)
            var teacherIds = gr.Select(x => x.Lesson.TeacherId)
                .Where(id => id != Guid.Empty)
                .Concat(gr.Select(x => x.Lesson.SecondTeacherId)
                    .Where(id => id.HasValue && id.Value != Guid.Empty)
                    .Select(id => id!.Value))
                .Distinct()
                .ToList();

            var journalName = $"{subjectNorm} - {group.Name} ({semester})";

            journalsToCreate.Add(new JournalEntry
            {
                Id = Guid.NewGuid(),
                Name = journalName,
                Subject = "Згенеровано автоматично",
                GroupId = groupId,
                TeacherId = teacherIds, // якщо в тебе тут Guid, а не List<Guid> — це треба міняти в моделі/БД
                MaxValue = 12,
                Date = DateTime.UtcNow,
                Comment = "Згенеровано автоматично"
            });

            // одразу додаємо ключ, щоб не створити дубль в рамках одного запуску
            existingKeys.Add(candidateKey);
        }

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

    private static string NormalizeSubjectName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var s = raw.Replace("  ", " ").Trim();

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

        static bool IsRomanOrNumber(string t)
        {
            var tt = t.Trim('.').ToLowerInvariant();
            return tt is "i" or "ii" or "і" or "іі" or "1" or "2";
        }

        static bool IsSubgroupWord(string t)
        {
            var tt = t.Trim('.').ToLowerInvariant();
            return tt is "підг" or "підгр" or "гр" or "група" or "р";
        }

        void TrimSubgroupTail()
        {
            if (tokens.Count == 0) return;

            string last = tokens[^1];
            string? prev = tokens.Count >= 2 ? tokens[^2] : null;

            bool lastIsNum = IsRomanOrNumber(last);
            bool lastIsSub = IsSubgroupWord(last);
            bool prevIsNum = prev != null && IsRomanOrNumber(prev);
            bool prevIsSub = prev != null && IsSubgroupWord(prev);

            // Двослівні хвости типу: "І р." або "підг 2"
            if (prev != null && ((prevIsNum && lastIsSub) || (prevIsSub && lastIsNum)))
            {
                tokens.RemoveAt(tokens.Count - 1);
                tokens.RemoveAt(tokens.Count - 1);
                return;
            }

            // Однослівні хвости: "р." або "підг"
            if (lastIsSub)
                tokens.RemoveAt(tokens.Count - 1);
        }

        // 1–2 проходи (на випадок "… І р.")
        TrimSubgroupTail();
        TrimSubgroupTail();

        var result = string.Join(' ', tokens).Trim();
        while (result.Contains("  ")) result = result.Replace("  ", " ");
        result = result.Trim(' ', ',', '.', '-', '–', '—');
        return result;
    }

    private static string ExtractSemesterSuffix(string name)
    {
        var start = name.LastIndexOf('(');
        var end = name.LastIndexOf(')');

        if (start >= 0 && end > start)
            return name.Substring(start + 1, end - start - 1).Trim();

        return "";
    }

    private static string GetSemesterForDate(DateTime lessonDate)
    {
        int m = lessonDate.Month, y = lessonDate.Year;
        if (m >= 9 && m <= 12) return $"1 семестр {y}/{y + 1}";
        if (m >= 1 && m <= 6) return $"2 семестр {y - 1}/{y}";
        return "Міжсезоння";
    }
}

public class GenerationResult
{
    public bool Success { get; set; }
    public int CreatedCount { get; set; }
    public string Message { get; set; } = "";
}
