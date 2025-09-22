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

    public LessonController(ILessonRepository lessonRepository,
                            ITeacherRepository teacherRepository,
                            IUserRepository userRepository,
                            IGroupRepository groupRepository)
    {
        _lessonRepository = lessonRepository;
        _teacherRepository = teacherRepository;
        _userRepository = userRepository;
        _groupRepository = groupRepository;
    }

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

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLessonRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (req.GroupId == Guid.Empty || req.TeacherId == Guid.Empty)
            return BadRequest("Потрібні GroupId та TeacherId.");
        if (req.StartTime == default)
            return BadRequest("Потрібна дата/час початку.");

        var baseLesson = new Lesson
        {
            Id = req.Id == Guid.Empty ? Guid.NewGuid() : req.Id,
            GroupId = req.GroupId,
            TeacherId = req.TeacherId,
            Name = req.Name ?? "",
            Topic = req.Topic ?? "",
            Homework = req.Homework ?? "",
            StartTime = req.StartTime,
            Clocks = null
        };

        // одиночный урок
        if (!req.RepeatWeekly || !req.EndDate.HasValue || req.EndDate.Value.Date < req.StartTime.Date)
        {
            await _lessonRepository.AddAsync(baseLesson);
            await _lessonRepository.SaveChangesAsync();
            return CreatedAtAction(nameof(GetById), new { id = baseLesson.Id }, baseLesson);
        }

        // серия по дню недели StartTime — щотижня до EndDate (включно)
        var list = new List<Lesson>();
        var startDate = req.StartTime.Date;
        var endDate = req.EndDate.Value.Date;
        var timeOfDay = req.StartTime.TimeOfDay;
        var dayOfWeek = req.StartTime.DayOfWeek;

        // первое попадание нужного дня недели
        var cursor = startDate;
        while (cursor.DayOfWeek != dayOfWeek) cursor = cursor.AddDays(1);

        for (var d = cursor; d <= endDate; d = d.AddDays(7))
        {
            list.Add(new Lesson
            {
                Id = Guid.NewGuid(),
                GroupId = req.GroupId,
                TeacherId = req.TeacherId,
                Name = req.Name ?? "",
                Topic = req.Topic ?? "",
                Homework = req.Homework ?? "",
                StartTime = d + timeOfDay,
                Clocks = null
            });
        }

        if (list.Count == 0)
        {
            await _lessonRepository.AddAsync(baseLesson);
            await _lessonRepository.SaveChangesAsync();
            return CreatedAtAction(nameof(GetById), new { id = baseLesson.Id }, baseLesson);
        }

        await _lessonRepository.AddRangeAsync(list);
        await _lessonRepository.SaveChangesAsync();

        return Ok(new { Created = list.Count, FirstId = list.First().Id });
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] Lesson updated)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var existing = await _lessonRepository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        if (!string.IsNullOrWhiteSpace(updated.Name))
            existing.Name = updated.Name;

        if (updated.GroupId != Guid.Empty && updated.GroupId != existing.GroupId)
            existing.GroupId = updated.GroupId;

        if (updated.TeacherId != Guid.Empty)
            existing.TeacherId = updated.TeacherId;

        // Second teacher: Guid.Empty → снять
        if (updated.SecondTeacherId.HasValue)
            existing.SecondTeacherId = updated.SecondTeacherId == Guid.Empty ? (Guid?)null : updated.SecondTeacherId;

        // null — не трогаем; "" — очистить
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

    [Authorize]
    [HttpPost("import")]
    public async Task<IActionResult> ImportLessons([FromForm] ImportLessonsDto dto)
    {
        if (dto.File == null || dto.File.Length == 0)
            return BadRequest("Файл порожній");

        // 1) Границы семестра
        var (semesterStart, semesterEnd) = GetSemesterBounds(DateTime.Today);

        // 2) Удаляем старое расписание внутри семестра для выбранной парности
        var allExistingLessons = await _lessonRepository.GetLessonsByGroupIdAsync(dto.GroupId);

        var lessonsToDelete = allExistingLessons
            .Where(l => l.StartTime.Date >= semesterStart.Date && l.StartTime.Date <= semesterEnd.Date)
            .Where(l =>
            {
                int weekNumber = (int)Math.Floor((l.StartTime.Date - semesterStart.Date).TotalDays / 7.0);
                bool isNumeratorWeek = (weekNumber % 2) == 0;
                return isNumeratorWeek == dto.IsNumerator;
            })
            .ToList();

        if (lessonsToDelete.Any())
        {
            await _lessonRepository.DeleteLessonsAsync(lessonsToDelete);
            await _lessonRepository.SaveChangesAsync();
        }

        // 3) Чтение Excel
        using var stream = new MemoryStream();
        await dto.File.CopyToAsync(stream);
        stream.Position = 0;
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.First();

        // Строка названий дней
        var dayColumns = ParseDayColumns(worksheet, 4);

        // 4) Поиск секций ЧИСЕЛЬНИК / ЗНАМЕННИК
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

        // 5) Парсинг строк выбранной секции
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
                    cellValue, dto, semesterStart, semesterEnd, currentPairNum, day);
                lessons.AddRange(lessonsFromCell);
            }
        }

        // 6) Сохраняем
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

    // Подпись слота исходного (базового) урока
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

        // Базовый день: реальная точка опоры
        var baselineOldList = (await _lessonRepository.GetLessonsByDateAsync(groupId, start))?.ToList()
                              ?? new List<Lesson>();
        var baselineById = baselineOldList.ToDictionary(l => l.Id, l => l);

        // Готовим маски изменений, исходя из конкретных baseline-Id
        var changes = new List<SlotChanges>();

        foreach (var newL in dto.Lessons)
        {
            if (!baselineById.TryGetValue(newL.Id, out var oldL))
            {
                // В базовый день нет такого слота — пропускаем
                continue;
            }

            var ch = new SlotChanges
            {
                Signature = SlotSignature.From(oldL), // подпись ДО изменений
                NewValues = newL,
                Delete = newL.Delete
            };

            if (!ch.Delete)
            {
                if (!string.Equals(oldL.Name ?? "", newL.Name ?? "", StringComparison.Ordinal)) ch.SetName = true;
                if (oldL.TeacherId != newL.TeacherId) ch.SetTeacherId = true;

                var oldT2 = SlotSignature.From(oldL).SecondTeacherId;
                var newT2 = newL.SecondTeacherId.HasValue && newL.SecondTeacherId != Guid.Empty ? newL.SecondTeacherId : null;
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

        // Идём неделями
        for (var d = start; d <= end; d = d.AddDays(7))
        {
            var dayLessons = (await _lessonRepository.GetLessonsByDateAsync(groupId, d))?.ToList();
            if (dayLessons == null || dayLessons.Count == 0) continue;

            foreach (var ch in changes)
            {
                // Ищем слот по подписи (старое состояние)
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

    /* ====================== ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ====================== */

    // Границы семестра.
    // • Июль–Ноябрь  -> 1-й семестр текущего года: 1.09.Y..31.12.Y
    // • Декабрь/Январь -> 2-й семестр след. года: 1.01.(Y+1)..30.06.(Y+1)
    // • Февраль–Июнь -> 2-й семестр текущего года: 1.01.Y..30.06.Y
    private (DateTime start, DateTime end) GetSemesterBounds(DateTime now)
    {
        int y = now.Year;

        if (now.Month >= 7 && now.Month <= 11)
        {
            var start = new DateTime(y, 9, 1);
            var end = new DateTime(y, 12, 31, 23, 59, 59);
            return (ShiftToMonday(start), end);
        }
        else if (now.Month == 12 || now.Month == 1)
        {
            var start = new DateTime(y + 1, 1, 1);
            var end = new DateTime(y + 1, 6, 30, 23, 59, 59);
            return (ShiftToMonday(start), end);
        }
        else
        {
            var start = new DateTime(y, 1, 1);
            var end = new DateTime(y, 6, 30, 23, 59, 59);
            return (ShiftToMonday(start), end);
        }
    }

    private DateTime ShiftToMonday(DateTime d)
    {
        while (d.DayOfWeek != DayOfWeek.Monday) d = d.AddDays(1);
        return d;
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
        DateTime semesterStart,
        DateTime semesterEnd,
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

        // 1) два предмета и два учителя → по одному Lesson на каждого
        if (subjects.Length > 1 && subjects.Length == teachers.Length)
        {
            for (int i = 0; i < subjects.Length; i++)
                result.AddRange(await CreateLessonsForWeeks(
                    subjects[i], teachers[i], null, dto, semesterStart, semesterEnd, pairNum, dayOfWeek));
        }
        // 2) один предмет, несколько учителей → делим на два Lesson (для подгрупп)
        else if (subjects.Length == 1 && teachers.Length > 1)
        {
            string subj = subjects[0];
            for (int i = 0; i < teachers.Length; i++)
                result.AddRange(await CreateLessonsForWeeks(
                    subj, teachers[i], null, dto, semesterStart, semesterEnd, pairNum, dayOfWeek));
        }
        // 3) обычный случай
        else
        {
            result.AddRange(await CreateLessonsForWeeks(
                subjects[0], teachers.First(), null, dto, semesterStart, semesterEnd, pairNum, dayOfWeek));
        }

        return result;
    }

    private async Task<IEnumerable<Lesson>> CreateLessonsForWeeks(
        string subject,
        string teacher1,
        string? teacher2,
        ImportLessonsDto dto,
        DateTime semesterStart,
        DateTime semesterEnd,
        string pairNum,
        DayOfWeek dayOfWeek)
    {
        var result = new List<Lesson>();
        if (!TryGetLessonStartTime(pairNum, out TimeOnly start)) return result;

        // Сдвиг дня в рамках недели (от понедельника)
        int offset = ((int)dayOfWeek - (int)DayOfWeek.Monday + 7) % 7;

        for (int week = 0; week < 18; week++)
        {
            var weekStart = semesterStart.AddDays(week * 7);
            if (weekStart.Date > semesterEnd.Date) break;

            bool isNumeratorWeek = (week % 2) == 0;
            if (!((dto.IsNumerator && isNumeratorWeek) || (!dto.IsNumerator && !isNumeratorWeek)))
                continue;

            var lessonDate = weekStart.AddDays(offset);
            if (lessonDate.Date > semesterEnd.Date) break;

            var teacherId1 = await _teacherRepository.GetTeacherIdByFullNameAsync(teacher1);

            var startDateTime = lessonDate.Date + start.ToTimeSpan();

            // Первый учитель (отдельный Lesson)
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

            // Если второй указан — создаём еще один Lesson (вторая подгруппа)
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

    // Мапа начала пары
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

    public class ImportLessonsDto
    {
        public IFormFile File { get; set; } = null!;
        public Guid GroupId { get; set; }
        public bool IsNumerator { get; set; } // true - для числителя, false - для знаменателя
    }

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

            var teacher = await _teacherRepository.GetByIdAsync(firstLesson.TeacherId);
            var user = await _userRepository.GetByIdAsync(firstLesson.TeacherId);

            worksheet.Cell("D2").Value = "Група:";
            worksheet.Cell("E2").Value = groupNameForHeader;
            worksheet.Cell("D4").Value = "Дисципліна:";
            worksheet.Cell("E4").Value = !string.IsNullOrEmpty(dto.SubjectName) ? dto.SubjectName : "Всі дисципліни";
            worksheet.Cell("D6").Value = "П.І.Б. викладача:";
            worksheet.Cell("E6").Value = user?.FullName ?? "Невідомий";

            // Заголовки таблицы
            var headerRow = 9;
            worksheet.Cell(headerRow, 1).Value = "Дата занять";
            worksheet.Cell(headerRow, 2).Value = "№ з/п";
            worksheet.Cell(headerRow, 3).Value = "Кількість годин";
            worksheet.Cell(headerRow, 4).Value = "Тема заняття";
            worksheet.Range(headerRow, 1, headerRow, 4).Style.Font.SetBold();
            worksheet.Range(headerRow, 1, headerRow, 4).Style.Fill.SetBackgroundColor(XLColor.LightGray);

            // Данные
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

    public class CreateLessonRequest
    {
        public Guid Id { get; set; }
        public Guid GroupId { get; set; }
        public Guid TeacherId { get; set; }
        public string Name { get; set; } = "";
        public DateTime StartTime { get; set; }
        public string? Topic { get; set; }
        public string? Homework { get; set; }
        public string? Subject { get; set; }

        public bool RepeatWeekly { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class ExportDto
    {
        public Guid? GroupId { get; set; }
        public Guid? TeacherId { get; set; }
        public string? SubjectName { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
