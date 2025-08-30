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
        // 1. Отримуємо всі групи, які мають розклад
        var groupsWithLessons = await _groupRepo.GetGroupsWithLessonsAsync();
        if (!groupsWithLessons.Any())
        {
            return new GenerationResult { Message = "Немає груп із розкладом для обробки." };
        }
        var groupsDict = groupsWithLessons.ToDictionary(g => g.Id);
        var groupIds = groupsDict.Keys;

        // 2. Отримуємо всі уроки для цих груп
        var allLessons = await _lessonRepo.GetAllAsync();
        var relevantLessons = allLessons.Where(l => groupIds.Contains(l.GroupId));

        // 3. Групуємо уроки за унікальною комбінацією (Предмет, Група, Викладач)
        var lessonGroups = relevantLessons
            .GroupBy(l => new { l.Name, l.GroupId, l.TeacherId });

        // 4. Отримуємо назви вже існуючих журналів
        var existingJournalSubjects = (await _journalRepo.GetJournalSubjectsByGroupIdsAsync(groupIds)).ToHashSet();

        var journalsToCreate = new List<JournalEntry>();

        // 5. Проходимо по групах уроків
        foreach (var groupOfLessons in lessonGroups)
        {
            var key = groupOfLessons.Key;
            var firstLessonInGroup = groupOfLessons.First(); // Беремо перший урок для визначення дати

            var group = groupsDict[key.GroupId];

            // Визначаємо семестр на основі дати уроку
            var semester = GetSemesterForDate(firstLessonInGroup.StartTime);

            // Формуємо стандартизовану назву журналу
            var journalSubject = $"{key.Name} - {group.Name} ({semester})";

            // Пропускаємо, якщо журнал із такою назвою вже існує
            if (existingJournalSubjects.Contains(journalSubject))
                continue;

            var newJournalEntry = new JournalEntry
            {
                Id = Guid.NewGuid(),
                Name = journalSubject,
                Subject = "Згенеровано автоматично",
                GroupId = key.GroupId,
                TeacherId = new List<Guid> { key.TeacherId },
                MaxValue = 12,
                Date = DateTime.UtcNow, // дата створення журналу
                Comment = "Згенеровано автоматично"
            };

            journalsToCreate.Add(newJournalEntry);
        }

        // 6. Зберігаємо всі нові журнали однією транзакцією
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

    // Хелпер для визначення семестру за датою уроку
    private string GetSemesterForDate(DateTime lessonDate)
    {
        int month = lessonDate.Month;
        int year = lessonDate.Year;

        // 1-й семестр: вересень – грудень
        if (month >= 9 && month <= 12)
        {
            return $"1 семестр {year}/{year + 1}";
        }
        // 2-й семестр: січень – червень
        else if (month >= 1 && month <= 6)
        {
            return $"2 семестр {year - 1}/{year}";
        }
        // Літні місяці
        else
        {
            return "Міжсезоння";
        }
    }
}

// Клас для результату генерації
public class GenerationResult
{
    public bool Success { get; set; }
    public int CreatedCount { get; set; }
    public string Message { get; set; }
}
