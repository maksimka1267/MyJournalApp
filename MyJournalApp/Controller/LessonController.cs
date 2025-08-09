using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
[ApiController]
[Route("api/[controller]")]
public class LessonController : ControllerBase
{
    private readonly ILessonRepository _lessonRepository;
    private readonly ITeacherRepository _teacherRepository;

    public LessonController(ILessonRepository lessonRepository, ITeacherRepository teacherRepository)
    {
        _lessonRepository = lessonRepository;
        _teacherRepository = teacherRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var lessons = await _lessonRepository.GetAllAsync();
        return Ok(lessons);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var lesson = await _lessonRepository.GetByIdAsync(id);
        if (lesson == null) return NotFound();
        return Ok(lesson);
    }

    [HttpGet("group/{groupId}")]
    public async Task<IActionResult> GetByGroup(Guid groupId)
    {
        var lessons = await _lessonRepository.GetLessonsByGroupIdAsync(groupId);
        return Ok(lessons);
    }

    [HttpGet("group/{groupId}/date/{date}")]
    public async Task<IActionResult> GetByGroupAndDate(Guid groupId, DateTime date)
    {

        var lessons = await _lessonRepository.GetLessonsByDateAsync(groupId, date);
        return Ok(lessons);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Lesson lesson)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        await _lessonRepository.AddAsync(lesson);
        await _lessonRepository.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = lesson.Id }, lesson);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] Lesson updated)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var existing = await _lessonRepository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        existing.Name = updated.Name;
        existing.GroupId = updated.GroupId;
        existing.TeacherId = updated.TeacherId;
        existing.Topic = updated.Topic;
        existing.Homework = updated.Homework;
        existing.StartTime = updated.StartTime;

        await _lessonRepository.Update(existing);
        return NoContent();
    }
    [HttpPost("import")]
    public async Task<IActionResult> ImportLessons([FromForm] ImportLessonsDto dto)
    {
        var file = dto.File;
        if (file == null || file.Length == 0)
            return BadRequest("Файл порожній");

        // Шаг 1: Определение начала семестра на основе текущей даты
        var today = DateTime.Today;
        var semesterStart = today;

        // Если текущая дата до 1 сентября, начало семестра - 1 сентября текущего года
        if (today.Month < 9)
        {
            semesterStart = new DateTime(today.Year, 9, 1);
        }
        // Если текущая дата после 1 января, но до 1 сентября,
        // начало семестра - 1 января текущего года
        else if (today.Month > 1) // Это условие уже не нужно, но для ясности оставим
        {
            // Логика остается прежней - берем текущий год
        }
        else if (today.Month < 1) // На самом деле это условие никогда не сработает, так как today всегда будет > 0
        {
            semesterStart = new DateTime(today.Year, 1, 1);
        }

        // Если текущая дата до 1 января, начало семестра - 1 января текущего года
        if (today < new DateTime(today.Year, 1, 1))
        {
            semesterStart = new DateTime(today.Year, 1, 1);
        }

        // Теперь находим первый понедельник, который равен или больше semesterStart
        while (semesterStart.DayOfWeek != DayOfWeek.Monday)
            semesterStart = semesterStart.AddDays(1);

        // Удаляем только старое расписание для нужного типа недели
        var allExistingLessons = await _lessonRepository.GetLessonsByGroupIdAsync(dto.GroupId);

        var lessonsToDelete = allExistingLessons.Where(l => {
            int weekNumber = (int)Math.Floor((l.StartTime.Date - semesterStart.Date).TotalDays / 7);
            bool isNumeratorWeek = weekNumber % 2 == 0;
            return isNumeratorWeek == dto.IsNumerator;
        }).ToList();

        if (lessonsToDelete.Any())
        {
            await _lessonRepository.DeleteLessonsAsync(lessonsToDelete);
            await _lessonRepository.SaveChangesAsync();
        }

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.First();

        // Находим колонки с днями недели (эта логика остается)
        var dayColumns = new Dictionary<string, int>();
        var daysRowIndex = 4;
        for (int col = 3; col <= worksheet.LastColumnUsed().ColumnNumber(); col++)
        {
            var day = worksheet.Cell(daysRowIndex, col).GetString().Trim();
            if (!string.IsNullOrEmpty(day))
                dayColumns[day] = col;
        }

        // ▼▼▼ НАЧАЛО НОВОЙ ЛОГИКИ ▼▼▼

        // Шаг 2: Находим строки, где начинаются секции "ЧИСЕЛЬНИК" и "ЗНАМЕННИК"
        int numeratorDataStartRow = 0;
        int denominatorDataStartRow = 0;
        int lastRow = worksheet.LastRowUsed().RowNumber();

        for (int r = 1; r <= lastRow; r++)
        {
            // Ищем заголовки секций во второй колонке (B)
            var headerCell = worksheet.Cell(r, 2).GetString().Trim();
            if (headerCell.Equals("ЧИСЕЛЬНИК", StringComparison.OrdinalIgnoreCase))
            {
                numeratorDataStartRow = r + 1; // Данные начинаются со следующей строки
            }
            else if (headerCell.Equals("ЗНАМЕННИК", StringComparison.OrdinalIgnoreCase))
            {
                denominatorDataStartRow = r + 1;
            }
        }

        // Шаг 3: Определяем, какой диапазон строк нужно читать
        int startRow;
        int endRow;

        if (dto.IsNumerator)
        {
            if (numeratorDataStartRow == 0) return BadRequest("Секція 'ЧИСЕЛЬНИК' не знайдена у файлі.");
            startRow = numeratorDataStartRow;
            // Конец секции - это либо начало знаменателя, либо конец файла
            endRow = (denominatorDataStartRow != 0) ? denominatorDataStartRow - 2 : lastRow;
        }
        else // Импортируем знаменатель
        {
            if (denominatorDataStartRow == 0) return BadRequest("Секція 'ЗНАМЕННИК' не знайдена у файлі.");
            startRow = denominatorDataStartRow;
            endRow = lastRow;
        }

        var lessons = new List<Lesson>();
        string currentPairNum = "";

        // Шаг 4: Читаем данные только из нужного диапазона строк
        for (int row = startRow; row <= endRow; row++)
        {
            var pairCell = worksheet.Cell(row, 2).GetString().Trim();
            if (!string.IsNullOrEmpty(pairCell))
            {
                currentPairNum = pairCell;
            }
            if (string.IsNullOrEmpty(currentPairNum) || currentPairNum.Equals("ПАРА", StringComparison.OrdinalIgnoreCase)) continue;

            foreach (var (day, col) in dayColumns)
            {
                var cellValue = worksheet.Cell(row, col).GetString().Trim();
                if (string.IsNullOrEmpty(cellValue) || cellValue == "_") continue;

                var lines = cellValue.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length < 2) continue; // Пропускаем, если нет и предмета, и преподавателя

                string subject = lines[0].Trim();
                string teacher = lines[1].Trim();

                if (!TryGetLessonStartTime(currentPairNum, out TimeOnly start)) continue;

                var dayOfWeek = day switch
                {
                    "ПОНЕДІЛОК" => DayOfWeek.Monday,
                    "ВІВТОРОК" => DayOfWeek.Tuesday,
                    "СЕРЕДА" => DayOfWeek.Wednesday,
                    "ЧЕТВЕР" => DayOfWeek.Thursday,
                    "П'ЯТНИЦЯ" => DayOfWeek.Friday,
                    _ => DayOfWeek.Monday
                };

                // Шаг 5: Создаем уроки для нужных недель (четных или нечетных)
                for (int week = 0; week < 18; week++)
                {
                    bool isNumeratorWeek = week % 2 == 0;

                    // Создаем урок, только если тип недели совпадает с запросом
                    if ((dto.IsNumerator && isNumeratorWeek) || (!dto.IsNumerator && !isNumeratorWeek))
                    {
                        var teacherId = await _teacherRepository.GetTeacherIdByFullNameAsync(teacher);
                        var baseDate = semesterStart.AddDays(week * 7);
                        var lessonDate = baseDate;
                        while (lessonDate.DayOfWeek != dayOfWeek)
                            lessonDate = lessonDate.AddDays(1);
                        var startDateTime = lessonDate.Date + start.ToTimeSpan();

                        lessons.Add(new Lesson
                        {
                            Id = Guid.NewGuid(),
                            Name = subject,
                            TeacherId = teacherId ?? Guid.Empty,
                            GroupId = dto.GroupId,
                            Topic = "",
                            Homework = "",
                            StartTime = startDateTime
                        });
                    }
                }
            }
        }

        // Шаг 6: Сохраняем результат
        await _lessonRepository.AddRangeAsync(lessons); // Используем AddRangeAsync для эффективности
        await _lessonRepository.SaveChangesAsync();

        return Ok(new { Count = lessons.Count });
    }

    // Метод TryGetLessonStartTime остается без изменений
    private bool TryGetLessonStartTime(string pairNumRaw, out TimeOnly start)
    {
        start = pairNumRaw switch
        {
            "І" => new TimeOnly(9, 0),
            "ІІ" => new TimeOnly(10, 10),
            "ІІІ" => new TimeOnly(11, 20),
            "IV" => new TimeOnly(12, 30),
            "V" => new TimeOnly(13, 40),
            _ => default
        };

        return pairNumRaw is "І" or "ІІ" or "ІІІ" or "IV" or "V";
    }
    public class ImportLessonsDto
    {
        public IFormFile File { get; set; }
        public Guid GroupId { get; set; }
        public bool IsNumerator { get; set; } // true - для числителя, false - для знаменателя
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var existing = await _lessonRepository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _lessonRepository.Delete(existing);
        return NoContent();
    }
}
