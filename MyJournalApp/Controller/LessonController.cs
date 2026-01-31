using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyJournalApp.Interface;
using MyJournalApp.Repository;

[ApiController]
[Route("api/[controller]")]
public class LessonController : ControllerBase
{
    private readonly ILessonRepository _lessonRepository;
    private readonly ITeacherRepository _teacherRepository;
    private readonly IUserRepository _userRepository;
    private readonly IGroupRepository _groupRepository;

    public LessonController(
        ILessonRepository lessonRepository,
        ITeacherRepository teacherRepository,
        IUserRepository userRepository,
        IGroupRepository groupRepository)
    {
        _lessonRepository = lessonRepository;
        _teacherRepository = teacherRepository;
        _userRepository = userRepository;
        _groupRepository = groupRepository;
    }

    /* ====================== GET ====================== */

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var lessons = await _lessonRepository.GetAllAsync();
        return Ok(lessons);
    }

    [Authorize]
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var lesson = await _lessonRepository.GetByIdAsync(id);
        if (lesson == null) return NotFound();
        return Ok(lesson);
    }

    [Authorize]
    [HttpGet("group/{groupId}")]
    public async Task<IActionResult> GetByGroup(Guid groupId)
    {
        var lessons = await _lessonRepository.GetLessonsByGroupIdAsync(groupId);
        return Ok(lessons);
    }

    [Authorize]
    [HttpGet("group/{groupId}/date/{date}")]
    public async Task<IActionResult> GetByGroupAndDate(Guid groupId, DateTime date)
    {
        var lessons = await _lessonRepository.GetLessonsByDateAsync(groupId, date);
        return Ok(lessons);
    }

    /* ====================== CREATE ====================== */

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLessonRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (req.GroupId == Guid.Empty || req.TeacherId == Guid.Empty)
            return BadRequest("Потрібні GroupId та TeacherId.");

        if (req.StartTime == default)
            return BadRequest("Потрібна дата/час початку.");

        // Нормалізація SecondTeacherId: Guid.Empty => null
        var secondTeacherId = (req.SecondTeacherId.HasValue && req.SecondTeacherId.Value != Guid.Empty)
            ? req.SecondTeacherId
            : null;

        // Базовий урок
        var baseLesson = new Lesson
        {
            Id = req.Id == Guid.Empty ? Guid.NewGuid() : req.Id,
            GroupId = req.GroupId,
            TeacherId = req.TeacherId,
            SecondTeacherId = secondTeacherId,     // ✅ другий викладач
            Name = req.Name ?? "",
            Topic = req.Topic ?? "",
            Homework = req.Homework ?? "",
            StartTime = req.StartTime,
            Clocks = req.Clocks                     // ✅ години (якщо потрібно)
        };

        // Одиночний урок
        if (!req.RepeatWeekly || !req.EndDate.HasValue || req.EndDate.Value.Date < req.StartTime.Date)
        {
            await _lessonRepository.AddAsync(baseLesson);
            await _lessonRepository.SaveChangesAsync();
            return CreatedAtAction(nameof(GetById), new { id = baseLesson.Id }, baseLesson);
        }

        // Серія: має бути хоча б одна галочка
        if (!req.ForNumerator && !req.ForDenominator)
            return BadRequest("Оберіть чисельник/знаменник або обидва.");

        var startDate = req.StartTime.Date;
        var endDate = req.EndDate.Value.Date;

        var timeOfDay = req.StartTime.TimeOfDay;
        var dayOfWeek = req.StartTime.DayOfWeek;

        // Перше попадання потрібного дня тижня (в межах/після startDate)
        var first = startDate;
        while (first.DayOfWeek != dayOfWeek) first = first.AddDays(1);

        if (first > endDate)
            return BadRequest("У заданому діапазоні немає дат для обраного дня тижня.");

        // Якір парності: понеділок тижня, в який потрапляє START (або first — тут однаково, бо same-week/next)
        var anchorMonday = StartOfWeekMonday(startDate);

        var list = new List<Lesson>();

        // Йдемо ЩОТИЖНЯ і фільтруємо по парності
        for (var d = first; d <= endDate; d = d.AddDays(7))
        {
            var weekIndex = (int)Math.Floor((d.Date - anchorMonday).TotalDays / 7.0);
            var isNumeratorWeek = (weekIndex % 2) == 0;

            if (isNumeratorWeek && !req.ForNumerator) continue;
            if (!isNumeratorWeek && !req.ForDenominator) continue;

            list.Add(new Lesson
            {
                Id = Guid.NewGuid(),
                GroupId = req.GroupId,
                TeacherId = req.TeacherId,
                SecondTeacherId = secondTeacherId,   // ✅ другий викладач у серії
                Name = req.Name ?? "",
                Topic = req.Topic ?? "",
                Homework = req.Homework ?? "",
                StartTime = d + timeOfDay,
                Clocks = req.Clocks
            });
        }

        if (list.Count == 0)
            return BadRequest("У заданому діапазоні немає дат для обраної парності.");

        await _lessonRepository.AddRangeAsync(list);
        await _lessonRepository.SaveChangesAsync();

        return Ok(new { Created = list.Count, FirstId = list.First().Id });
    }

    /* ====================== UPDATE ====================== */

    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] Lesson updated)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var existing = await _lessonRepository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        // Назву предмету оновлюємо тільки якщо не порожня
        if (!string.IsNullOrWhiteSpace(updated.Name))
            existing.Name = updated.Name;

        if (updated.GroupId != Guid.Empty && updated.GroupId != existing.GroupId)
            existing.GroupId = updated.GroupId;

        if (updated.TeacherId != Guid.Empty)
            existing.TeacherId = updated.TeacherId;

        // Другий викладач: Guid.Empty → зняти
        if (updated.SecondTeacherId.HasValue)
            existing.SecondTeacherId = updated.SecondTeacherId == Guid.Empty ? (Guid?)null : updated.SecondTeacherId;

        // null — не чіпаємо; "" — очистити
        if (updated.Topic != null)
            existing.Topic = updated.Topic;

        if (updated.Homework != null)
            existing.Homework = updated.Homework;

        if (updated.Clocks.HasValue)
            existing.Clocks = updated.Clocks;

        if (updated.StartTime != default)
            existing.StartTime = updated.StartTime;

        await _lessonRepository.Update(existing);
        await _lessonRepository.SaveChangesAsync();

        return NoContent();
    }

    /* ====================== IMPORT (Excel) ====================== */

    [Authorize]
    [HttpPost("import")]
    public async Task<IActionResult> ImportLessons([FromForm] ImportLessonsDto dto)
    {
        if (dto.File == null || dto.File.Length == 0)
            return BadRequest("Файл порожній");

        if (dto.GroupId == Guid.Empty)
            return BadRequest("Потрібен GroupId.");

        // Діапазон імпорту з UI
        var rangeStart = dto.StartDate.Date;
        var rangeEnd = dto.EndDate.Date;

        if (rangeStart == default || rangeEnd == default)
            return BadRequest("Потрібні StartDate та EndDate.");

        if (rangeEnd < rangeStart)
            return BadRequest("Кінцева дата раніше за початкову.");

        // Якір парності: понеділок тижня StartDate
        var anchorMonday = StartOfWeekMonday(rangeStart);

        // 1) Видаляємо старий розклад у межах діапазону тільки для обраної парності
        var allExistingLessons = await _lessonRepository.GetLessonsByGroupIdAsync(dto.GroupId);

        var lessonsToDelete = allExistingLessons
            .Where(l => l.StartTime.Date >= rangeStart && l.StartTime.Date <= rangeEnd)
            .Where(l =>
            {
                var weekNumber = (int)Math.Floor((l.StartTime.Date - anchorMonday).TotalDays / 7.0);
                var isNumeratorWeek = (weekNumber % 2) == 0;
                return isNumeratorWeek == dto.IsNumerator;
            })
            .ToList();

        if (lessonsToDelete.Any())
        {
            await _lessonRepository.DeleteLessonsAsync(lessonsToDelete);
            await _lessonRepository.SaveChangesAsync();
        }

        // 2) Читаємо Excel
        using var stream = new MemoryStream();
        await dto.File.CopyToAsync(stream);
        stream.Position = 0;

        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.First();

        // Рядок з назвами днів
        var dayColumns = ParseDayColumns(worksheet, 4);

        // 3) Знаходимо секції ЧИСЕЛЬНИК / ЗНАМЕННИК
        var (numeratorRow, denominatorRow, lastRow) = FindSectionRows(worksheet);

        int startRow, endRow;
        if (dto.IsNumerator)
        {
            if (numeratorRow == 0) return BadRequest("Секція 'ЧИСЕЛЬНИК' не знайдена у файлі.");
            startRow = numeratorRow;
            endRow = (denominatorRow != 0) ? denominatorRow - 2 : lastRow;
        }
        else
        {
            if (denominatorRow == 0) return BadRequest("Секція 'ЗНАМЕННИК' не знайдена у файлі.");
            startRow = denominatorRow;
            endRow = lastRow;
        }

        // 4) Парсимо вибрану секцію
        var lessons = new List<Lesson>();
        string currentPairNum = "";

        for (int row = startRow; row <= endRow; row++)
        {
            var pairCell = worksheet.Cell(row, 2).GetString().Trim();
            if (!string.IsNullOrEmpty(pairCell))
                currentPairNum = pairCell;

            if (string.IsNullOrEmpty(currentPairNum) ||
                currentPairNum.Equals("ПАРА", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var (day, col) in dayColumns)
            {
                var cellValue = worksheet.Cell(row, col).GetString().Trim();
                if (string.IsNullOrEmpty(cellValue) || cellValue == "_") continue;

                var lessonsFromCell = await ParseCell(
                    cellValue, dto, anchorMonday, rangeStart, rangeEnd, currentPairNum, day);

                lessons.AddRange(lessonsFromCell);
            }
        }

        // 5) Зберігаємо
        if (lessons.Count > 0)
        {
            await _lessonRepository.AddRangeAsync(lessons);
            await _lessonRepository.SaveChangesAsync();
        }

        return Ok(new { Count = lessons.Count });
    }

    /* ====================== BULK-APPLY (fixed for subgroups) ====================== */

    public class BulkApplyDto
    {
        public List<BulkApplyLessonDto> Lessons { get; set; } = new();
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class BulkApplyLessonDto
    {
        public Guid Id { get; set; }
        public Guid GroupId { get; set; }
        public DateTime StartTime { get; set; }

        public string? Name { get; set; }
        public string? Topic { get; set; }
        public string? Homework { get; set; }
        public int? Clocks { get; set; }
        public Guid TeacherId { get; set; }
        public Guid? SecondTeacherId { get; set; }

        public bool Delete { get; set; }
    }

    // Підпис слота базового уроку (до змін)
    private sealed class SlotSignature
    {
        public TimeSpan Time { get; init; }
        public string Name { get; init; } = "";
        public Guid TeacherId { get; init; }
        public Guid? SecondTeacherId { get; init; }

        public static SlotSignature From(Lesson l) => new()
        {
            Time = l.StartTime.TimeOfDay,
            Name = l.Name ?? "",
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
        public BulkApplyLessonDto? NewValues { get; init; }
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

    [Authorize(Roles = "Admin")]
    [HttpPost("bulk-apply")]
    public async Task<IActionResult> BulkApply([FromBody] BulkApplyDto dto)
    {
        if (dto == null || dto.Lessons == null || dto.Lessons.Count == 0)
            return BadRequest("Порожній пакет уроків.");

        var start = dto.StartDate.Date;
        var end = dto.EndDate.Date;
        if (end < start) return BadRequest("Кінцева дата раніше за початкову.");

        var groupIds = dto.Lessons.Select(l => l.GroupId).Distinct().ToList();
        if (groupIds.Count != 1) return BadRequest("Усі уроки мають бути однієї групи.");
        var groupId = groupIds[0];

        // Базовий день: реальна точка опори
        var baselineOldList = (await _lessonRepository.GetLessonsByDateAsync(groupId, start))?.ToList()
                              ?? new List<Lesson>();
        var baselineById = baselineOldList.ToDictionary(l => l.Id, l => l);

        // Готуємо маски змін, виходячи з конкретних baseline-Id
        var changes = new List<SlotChanges>();

        foreach (var newL in dto.Lessons)
        {
            if (!baselineById.TryGetValue(newL.Id, out var oldL))
            {
                // У базовий день немає такого слота — пропускаємо
                continue;
            }

            var ch = new SlotChanges
            {
                Signature = SlotSignature.From(oldL), // підпис ДО змін
                NewValues = newL,
                Delete = newL.Delete
            };

            if (!ch.Delete)
            {
                if (!string.Equals(oldL.Name ?? "", newL.Name ?? "", StringComparison.Ordinal)) ch.SetName = true;
                if (oldL.TeacherId != newL.TeacherId) ch.SetTeacherId = true;

                var oldT2 = SlotSignature.From(oldL).SecondTeacherId;
                var newT2 = (newL.SecondTeacherId.HasValue && newL.SecondTeacherId != Guid.Empty) ? newL.SecondTeacherId : null;
                if (oldT2 != newT2) ch.SetSecondTeacherId = true;

                if (!string.Equals(oldL.Topic ?? "", newL.Topic ?? "", StringComparison.Ordinal)) ch.SetTopic = true;
                if (!string.Equals(oldL.Homework ?? "", newL.Homework ?? "", StringComparison.Ordinal)) ch.SetHomework = true;
                if (oldL.Clocks != newL.Clocks) ch.SetClocks = true;
            }

            if (ch.Delete || ch.HasAny)
                changes.Add(ch);
        }

        if (changes.Count == 0)
            return Ok(new { Updated = 0, Deleted = 0 });

        int updated = 0, deleted = 0;

        // Йдемо тижнями
        for (var d = start; d <= end; d = d.AddDays(7))
        {
            var dayLessons = (await _lessonRepository.GetLessonsByDateAsync(groupId, d))?.ToList();
            if (dayLessons == null || dayLessons.Count == 0) continue;

            foreach (var ch in changes)
            {
                // Шукаємо слот по підпису (старий стан)
                var target = dayLessons.FirstOrDefault(l => ch.Signature.Matches(l));
                if (target == null) continue;

                if (ch.Delete)
                {
                    await _lessonRepository.Delete(target);
                    deleted++;
                    continue;
                }

                var nv = ch.NewValues!;
                if (ch.SetName) target.Name = nv.Name;
                if (ch.SetTeacherId) target.TeacherId = nv.TeacherId;
                if (ch.SetSecondTeacherId)
                    target.SecondTeacherId = (nv.SecondTeacherId.HasValue && nv.SecondTeacherId != Guid.Empty) ? nv.SecondTeacherId : null;
                if (ch.SetTopic) target.Topic = nv.Topic;
                if (ch.SetHomework) target.Homework = nv.Homework;
                if (ch.SetClocks) target.Clocks = nv.Clocks;

                await _lessonRepository.Update(target);
                updated++;
            }
        }

        await _lessonRepository.SaveChangesAsync();
        return Ok(new { Updated = updated, Deleted = deleted });
    }

    /* ====================== SUBJECTS ====================== */

    [Authorize(Roles = "Admin")]
    [HttpGet("group/{groupId}/subjects")]
    public async Task<IActionResult> GetSubjectsByGroup(Guid groupId)
    {
        var lessons = await _lessonRepository.GetLessonsByGroupIdAsync(groupId);

        var subjects = lessons
            .Select(l => l.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct()
            .OrderBy(n => n)
            .ToList();

        return Ok(subjects);
    }

    /* ====================== EXPORT ====================== */

    [Authorize]
    [HttpGet("export")]
    public async Task<IActionResult> ExportToExcel([FromQuery] ExportDto dto)
    {
        if (!dto.TeacherId.HasValue || !dto.StartDate.HasValue || !dto.EndDate.HasValue)
        {
            return BadRequest("Необходимо указать преподавателя и полный период (начальная и конечная даты) для формирования отчета.");
        }

        var filteredLessons = await _lessonRepository.GetByTeacherAsync(
            dto.TeacherId.Value,
            dto.StartDate.Value,
            dto.EndDate.Value,
            dto.GroupId,
            dto.SubjectName
        );

        if (filteredLessons.Count == 0)
        {
            return NotFound("Нет уроков, соответствующих вашим критериям.");
        }

        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Звіт по годинах");

            var firstLesson = filteredLessons.First();
            string groupNameForHeader = "Всі групи";
            Guid? effectiveGroupId = dto.GroupId;

            if (!effectiveGroupId.HasValue)
            {
                var distinctGroups = filteredLessons.Select(l => l.GroupId).Distinct().ToList();
                if (distinctGroups.Count == 1) effectiveGroupId = distinctGroups[0];
            }
            if (effectiveGroupId.HasValue)
            {
                var group = await _groupRepository.GetByIdAsync(effectiveGroupId.Value);
                groupNameForHeader = group?.Name ?? "Невідома група";
            }

            var user = await _userRepository.GetByIdAsync(firstLesson.TeacherId);

            worksheet.Cell("D2").Value = "Група:";
            worksheet.Cell("E2").Value = groupNameForHeader;
            worksheet.Cell("D4").Value = "Дисципліна:";
            worksheet.Cell("E4").Value = !string.IsNullOrEmpty(dto.SubjectName) ? dto.SubjectName : "Всі дисципліни";
            worksheet.Cell("D6").Value = "П.І.Б. викладача:";
            worksheet.Cell("E6").Value = user?.FullName ?? "Невідомий";

            // Заголовки таблиці
            var headerRow = 9;
            worksheet.Cell(headerRow, 1).Value = "Дата занять";
            worksheet.Cell(headerRow, 2).Value = "№ з/п";
            worksheet.Cell(headerRow, 3).Value = "Кількість годин";
            worksheet.Cell(headerRow, 4).Value = "Тема заняття";
            worksheet.Range(headerRow, 1, headerRow, 4).Style.Font.SetBold();
            worksheet.Range(headerRow, 1, headerRow, 4).Style.Fill.SetBackgroundColor(XLColor.LightGray);

            // Дані
            int currentRow = headerRow + 1;
            int lessonNumber = 1;
            foreach (var lesson in filteredLessons)
            {
                worksheet.Cell(currentRow, 1).Value = lesson.StartTime.ToString("yyyy-MM-dd");
                worksheet.Cell(currentRow, 2).Value = lessonNumber++;
                worksheet.Cell(currentRow, 3).Value = lesson.Clocks.HasValue ? lesson.Clocks.Value.ToString() : "N/A";
                worksheet.Cell(currentRow, 4).Value = lesson.Topic;
                currentRow++;
            }

            worksheet.Column(1).AdjustToContents();
            worksheet.Column(2).AdjustToContents();
            worksheet.Column(3).AdjustToContents();
            worksheet.Column(4).Width = 50;

            using (var outStream = new MemoryStream())
            {
                workbook.SaveAs(outStream);
                var content = outStream.ToArray();
                var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                var fileName = $"Export_{user?.FullName}_{DateTime.Now:yyyyMMdd}.xlsx";

                return File(content, contentType, fileName);
            }
        }
    }

    /* ====================== DELETE ====================== */

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var existing = await _lessonRepository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _lessonRepository.Delete(existing);
        await _lessonRepository.SaveChangesAsync();
        return NoContent();
    }

    /* ====================== DTOs ====================== */

    public class ImportLessonsDto
    {
        public IFormFile File { get; set; } = null!;
        public Guid GroupId { get; set; }
        public bool IsNumerator { get; set; } // true - для чисельника, false - для знаменника

        public DateTime StartDate { get; set; } // тільки дата
        public DateTime EndDate { get; set; }   // тільки дата
    }

    public class CreateLessonRequest
    {
        public Guid Id { get; set; }
        public Guid GroupId { get; set; }
        public Guid TeacherId { get; set; }

        // НОВЕ
        public Guid? SecondTeacherId { get; set; }

        public string Name { get; set; } = "";
        public DateTime StartTime { get; set; }
        public string? Topic { get; set; }
        public string? Homework { get; set; }

        // Якщо потрібно — додай
        public int? Clocks { get; set; }

        public bool RepeatWeekly { get; set; }
        public DateTime? EndDate { get; set; }

        // ВАЖЛИВО: без дефолтів true/true
        public bool ForNumerator { get; set; } = false;
        public bool ForDenominator { get; set; } = false;
    }


    public class ExportDto
    {
        public Guid? GroupId { get; set; }
        public Guid? TeacherId { get; set; }
        public string? SubjectName { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    /* ====================== HELPERS (Import) ====================== */

    // Понеділок тижня, в який потрапляє дата (якір для парності)
    private static DateTime StartOfWeekMonday(DateTime date)
    {
        var d = date.Date;
        int diff = (7 + (int)d.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        return d.AddDays(-diff);
    }

    private (int numeratorRow, int denominatorRow, int lastRow) FindSectionRows(IXLWorksheet worksheet)
    {
        int numeratorDataStartRow = 0;
        int denominatorDataStartRow = 0;
        int lastRow = worksheet.LastRowUsed().RowNumber();

        for (int r = 1; r <= lastRow; r++)
        {
            var headerCell = worksheet.Cell(r, 2).GetString().Trim();
            if (headerCell.Equals("ЧИСЕЛЬНИК", StringComparison.OrdinalIgnoreCase))
                numeratorDataStartRow = r + 1;
            else if (headerCell.Equals("ЗНАМЕННИК", StringComparison.OrdinalIgnoreCase))
                denominatorDataStartRow = r + 1;
        }

        return (numeratorDataStartRow, denominatorDataStartRow, lastRow);
    }

    private Dictionary<string, int> ParseDayColumns(IXLWorksheet worksheet, int daysRowIndex)
    {
        var dayColumns = new Dictionary<string, int>();
        for (int col = 3; col <= worksheet.LastColumnUsed().ColumnNumber(); col++)
        {
            var day = worksheet.Cell(daysRowIndex, col).GetString().Trim();
            if (!string.IsNullOrEmpty(day))
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
        if (lines.Length < 2) return result;

        var subjects = lines[0].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();
        var teachers = lines[1].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();

        var dayOfWeek = day switch
        {
            "ПОНЕДІЛОК" => DayOfWeek.Monday,
            "ВІВТОРОК" => DayOfWeek.Tuesday,
            "СЕРЕДА" => DayOfWeek.Wednesday,
            "ЧЕТВЕР" => DayOfWeek.Thursday,
            "П'ЯТНИЦЯ" => DayOfWeek.Friday,
            _ => DayOfWeek.Monday
        };

        // 1) два предмети і два викладачі → по одному Lesson на кожного
        if (subjects.Length > 1 && subjects.Length == teachers.Length)
        {
            for (int i = 0; i < subjects.Length; i++)
            {
                result.AddRange(await CreateLessonsForWeeks(
                    subjects[i], teachers[i], null, dto, anchorMonday, rangeStart, rangeEnd, pairNum, dayOfWeek));
            }
        }
        // 2) один предмет, кілька викладачів → по одному Lesson на кожного (підгрупи)
        else if (subjects.Length == 1 && teachers.Length > 1)
        {
            string subj = subjects[0];
            for (int i = 0; i < teachers.Length; i++)
            {
                result.AddRange(await CreateLessonsForWeeks(
                    subj, teachers[i], null, dto, anchorMonday, rangeStart, rangeEnd, pairNum, dayOfWeek));
            }
        }
        // 3) звичайний випадок
        else
        {
            result.AddRange(await CreateLessonsForWeeks(
                subjects[0], teachers.First(), null, dto, anchorMonday, rangeStart, rangeEnd, pairNum, dayOfWeek));
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
        if (!TryGetLessonStartTime(pairNum, out TimeOnly start)) return result;

        // Перша дата потрібного дня тижня в межах діапазону
        var first = rangeStart.Date;
        while (first.DayOfWeek != dayOfWeek) first = first.AddDays(1);
        if (first > rangeEnd.Date) return result;

        for (var d = first; d <= rangeEnd.Date; d = d.AddDays(7))
        {
            var weekNumber = (int)Math.Floor((d.Date - anchorMonday.Date).TotalDays / 7.0);
            var isNumeratorWeek = (weekNumber % 2) == 0;

            if (isNumeratorWeek != dto.IsNumerator) continue;

            var teacherId1 = await _teacherRepository.GetTeacherIdByFullNameAsync(teacher1);
            var startDateTime = d.Date + start.ToTimeSpan();

            // Перший викладач (окремий Lesson)
            result.Add(new Lesson
            {
                Id = Guid.NewGuid(),
                Name = subject,
                TeacherId = teacherId1 ?? Guid.Empty,
                GroupId = dto.GroupId,
                Topic = "",
                Homework = "",
                StartTime = startDateTime,
                Clocks = null
            });

            // Якщо є другий викладач — створюємо ще один Lesson (інша підгрупа)
            if (!string.IsNullOrWhiteSpace(teacher2))
            {
                var teacherId2 = await _teacherRepository.GetTeacherIdByFullNameAsync(teacher2);
                if (teacherId2.HasValue && teacherId2.Value != Guid.Empty)
                {
                    result.Add(new Lesson
                    {
                        Id = Guid.NewGuid(),
                        Name = subject,
                        TeacherId = teacherId2.Value,
                        GroupId = dto.GroupId,
                        Topic = "",
                        Homework = "",
                        StartTime = startDateTime,
                        Clocks = null
                    });
                }
            }
        }

        return result;
    }

    // Мапа початку пари
    private bool TryGetLessonStartTime(string pairNumRaw, out TimeOnly start)
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
