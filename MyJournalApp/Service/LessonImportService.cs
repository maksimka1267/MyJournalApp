using ClosedXML.Excel;
using MyJournalApp.Data.Dtos.Lesson;
using MyJournalApp.Service.Interface;

namespace MyJournalApp.Service;

public class LessonImportService : ILessonImportService
{
    private readonly ILessonRepository _lessonRepository;
    private readonly ITeacherRepository _teacherRepository;

    public LessonImportService(
        ILessonRepository lessonRepository,
        ITeacherRepository teacherRepository)
    {
        _lessonRepository = lessonRepository;
        _teacherRepository = teacherRepository;
    }

    public async Task<ImportResultDto> ImportAsync(ImportLessonsDto dto)
    {
        if (dto.File == null || dto.File.Length == 0)
            throw new ArgumentException("Файл порожній");

        if (dto.GroupId == Guid.Empty)
            throw new ArgumentException("Потрібен GroupId.");

        var rangeStart = dto.StartDate.Date;
        var rangeEnd = dto.EndDate.Date;

        if (rangeStart == default || rangeEnd == default)
            throw new ArgumentException("Потрібні StartDate та EndDate.");

        if (rangeEnd < rangeStart)
            throw new ArgumentException("Кінцева дата раніше за початкову.");

        var anchorMonday = StartOfWeekMonday(rangeStart);

        var allExistingLessons =
            await _lessonRepository.GetLessonsByGroupIdAsync(dto.GroupId);

        var lessonsToDelete = allExistingLessons
            .Where(l => l.StartTime.Date >= rangeStart &&
                        l.StartTime.Date <= rangeEnd)
            .Where(l =>
            {
                var weekNumber =
                    (int)Math.Floor(
                        (l.StartTime.Date - anchorMonday).TotalDays / 7.0);

                var isNumeratorWeek = weekNumber % 2 == 0;

                return isNumeratorWeek == dto.IsNumerator;
            })
            .ToList();

        if (lessonsToDelete.Any())
        {
            await _lessonRepository.DeleteLessonsAsync(lessonsToDelete);
            await _lessonRepository.SaveChangesAsync();
        }

        using var stream = new MemoryStream();

        await dto.File.CopyToAsync(stream);

        stream.Position = 0;

        using var workbook = new XLWorkbook(stream);

        var worksheet = workbook.Worksheets.First();

        var dayColumns = ParseDayColumns(worksheet, 4);

        var (numeratorRow, denominatorRow, lastRow) =
            FindSectionRows(worksheet);

        int startRow;
        int endRow;

        if (dto.IsNumerator)
        {
            if (numeratorRow == 0)
                throw new ArgumentException(
                    "Секція 'ЧИСЕЛЬНИК' не знайдена.");

            startRow = numeratorRow;
            endRow = denominatorRow != 0
                ? denominatorRow - 2
                : lastRow;
        }
        else
        {
            if (denominatorRow == 0)
                throw new ArgumentException(
                    "Секція 'ЗНАМЕННИК' не знайдена.");

            startRow = denominatorRow;
            endRow = lastRow;
        }

        var lessons = new List<Lesson>();

        string currentPair = "";

        for (int row = startRow; row <= endRow; row++)
        {
            var pairCell = worksheet.Cell(row, 2)
                .GetString()
                .Trim();

            if (!string.IsNullOrEmpty(pairCell))
                currentPair = pairCell;

            if (string.IsNullOrEmpty(currentPair) ||
                currentPair.Equals("ПАРА",
                    StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var (day, col) in dayColumns)
            {
                var value = worksheet
                    .Cell(row, col)
                    .GetString()
                    .Trim();

                if (string.IsNullOrWhiteSpace(value) || value == "_")
                    continue;

                var parsed =
                    await ParseCell(
                        value,
                        dto,
                        anchorMonday,
                        rangeStart,
                        rangeEnd,
                        currentPair,
                        day);

                lessons.AddRange(parsed);
            }
        }

        if (lessons.Any())
        {
            await _lessonRepository.AddRangeAsync(lessons);
            await _lessonRepository.SaveChangesAsync();
        }

        return new ImportResultDto
        {
            Count = lessons.Count
        };
    }
    private static DateTime StartOfWeekMonday(DateTime date)
    {
        var d = date.Date;
        int diff = (7 + (int)d.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        return d.AddDays(-diff);
    }
    private static (int numeratorRow, int denominatorRow, int lastRow)
    FindSectionRows(IXLWorksheet worksheet)
    {
        int numeratorDataStartRow = 0;
        int denominatorDataStartRow = 0;
        int lastRow = worksheet.LastRowUsed().RowNumber();

        for (int r = 1; r <= lastRow; r++)
        {
            var value = worksheet.Cell(r, 2).GetString().Trim();

            if (value.Equals("ЧИСЕЛЬНИК", StringComparison.OrdinalIgnoreCase))
                numeratorDataStartRow = r + 1;
            else if (value.Equals("ЗНАМЕННИК", StringComparison.OrdinalIgnoreCase))
                denominatorDataStartRow = r + 1;
        }

        return (numeratorDataStartRow, denominatorDataStartRow, lastRow);
    }
    private static Dictionary<string, int> ParseDayColumns(
    IXLWorksheet worksheet,
    int daysRowIndex)
    {
        var dayColumns = new Dictionary<string, int>();

        for (int col = 3; col <= worksheet.LastColumnUsed().ColumnNumber(); col++)
        {
            var day = worksheet.Cell(daysRowIndex, col)
                .GetString()
                .Trim();

            if (!string.IsNullOrWhiteSpace(day))
                dayColumns[day] = col;
        }

        return dayColumns;
    }
    private async Task<IEnumerable<Lesson>> ParseCell(
    string cellValue,
    ImportLessonsDto dto,
    DateTime anchorMonday,
    DateTime rangeStart,
    DateTime rangeEnd,
    string pairNum,
    string day)
    {
        var result = new List<Lesson>();

        var lines = cellValue.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length < 2)
            return result;

        var subjects = lines[0]
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .ToArray();

        var teachers = lines[1]
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .ToArray();

        var dayOfWeek = day switch
        {
            "ПОНЕДІЛОК" => DayOfWeek.Monday,
            "ВІВТОРОК" => DayOfWeek.Tuesday,
            "СЕРЕДА" => DayOfWeek.Wednesday,
            "ЧЕТВЕР" => DayOfWeek.Thursday,
            "П'ЯТНИЦЯ" => DayOfWeek.Friday,
            _ => DayOfWeek.Monday
        };

        // два предмети + два викладачі
        if (subjects.Length > 1 && subjects.Length == teachers.Length)
        {
            for (int i = 0; i < subjects.Length; i++)
            {
                result.AddRange(await CreateLessonsForWeeks(
                    subjects[i],
                    teachers[i],
                    null,
                    dto,
                    anchorMonday,
                    rangeStart,
                    rangeEnd,
                    pairNum,
                    dayOfWeek));
            }
        }
        // один предмет + кілька викладачів
        else if (subjects.Length == 1 && teachers.Length > 1)
        {
            foreach (var teacher in teachers)
            {
                result.AddRange(await CreateLessonsForWeeks(
                    subjects[0],
                    teacher,
                    null,
                    dto,
                    anchorMonday,
                    rangeStart,
                    rangeEnd,
                    pairNum,
                    dayOfWeek));
            }
        }
        // звичайний випадок
        else
        {
            result.AddRange(await CreateLessonsForWeeks(
                subjects[0],
                teachers.First(),
                null,
                dto,
                anchorMonday,
                rangeStart,
                rangeEnd,
                pairNum,
                dayOfWeek));
        }

        return result;
    }
    private async Task<IEnumerable<Lesson>> CreateLessonsForWeeks(
    string subject,
    string teacher1,
    string? teacher2,
    ImportLessonsDto dto,
    DateTime anchorMonday,
    DateTime rangeStart,
    DateTime rangeEnd,
    string pairNum,
    DayOfWeek dayOfWeek)
    {
        var result = new List<Lesson>();

        if (!TryGetLessonStartTime(pairNum, out TimeOnly start))
            return result;

        // Перша дата потрібного дня тижня
        var first = rangeStart.Date;

        while (first.DayOfWeek != dayOfWeek)
            first = first.AddDays(1);

        if (first > rangeEnd.Date)
            return result;

        for (var d = first; d <= rangeEnd.Date; d = d.AddDays(7))
        {
            var weekNumber =
                (int)Math.Floor((d.Date - anchorMonday.Date).TotalDays / 7.0);

            var isNumeratorWeek = (weekNumber % 2) == 0;

            if (isNumeratorWeek != dto.IsNumerator)
                continue;

            var teacherId1 =
                await _teacherRepository.GetTeacherIdByFullNameAsync(teacher1);

            var startDateTime = d.Date + start.ToTimeSpan();

            result.Add(new Lesson
            {
                Id = Guid.NewGuid(),
                Name = subject,
                TeacherId = teacherId1 ?? Guid.Empty,
                GroupId = dto.GroupId,
                Topic = string.Empty,
                Homework = string.Empty,
                StartTime = startDateTime,
                Clocks = null
            });

            if (!string.IsNullOrWhiteSpace(teacher2))
            {
                var teacherId2 =
                    await _teacherRepository.GetTeacherIdByFullNameAsync(teacher2);

                if (teacherId2.HasValue && teacherId2.Value != Guid.Empty)
                {
                    result.Add(new Lesson
                    {
                        Id = Guid.NewGuid(),
                        Name = subject,
                        TeacherId = teacherId2.Value,
                        GroupId = dto.GroupId,
                        Topic = string.Empty,
                        Homework = string.Empty,
                        StartTime = startDateTime,
                        Clocks = null
                    });
                }
            }
        }

        return result;
    }
    private static bool TryGetLessonStartTime(
    string pairNumRaw,
    out TimeOnly start)
    {
        start = pairNumRaw switch
        {
            "1" => new TimeOnly(9, 0),
            "2" => new TimeOnly(10, 10),
            "3" => new TimeOnly(11, 20),
            "4" => new TimeOnly(12, 30),
            "5" => new TimeOnly(13, 40),
            "6" => new TimeOnly(14, 50),
            "7" => new TimeOnly(16, 0),
            "8" => new TimeOnly(17, 10),
            _ => default
        };

        return start != default;
    }
}