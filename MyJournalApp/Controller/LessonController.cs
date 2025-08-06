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

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.First();

        var dayColumns = new Dictionary<string, int>();
        var daysRowIndex = 4;
        var pairRowIndex = 5;

        for (int col = 3; col <= worksheet.LastColumnUsed().ColumnNumber(); col++)
        {
            var day = worksheet.Cell(daysRowIndex, col).GetString().Trim();
            if (!string.IsNullOrEmpty(day))
                dayColumns[day] = col;
        }

        var lessons = new List<Lesson>();

        for (int row = pairRowIndex; row <= worksheet.LastRowUsed().RowNumber(); row++)
        {
            var pairNumRaw = worksheet.Cell(row, 2).GetString().Trim(); // Вторая колонка — номер пары
            if (string.IsNullOrEmpty(pairNumRaw)) continue;

            foreach (var (day, col) in dayColumns)
            {
                var cellValue = worksheet.Cell(row, col).GetString().Trim();
                if (string.IsNullOrEmpty(cellValue) || cellValue == "_") continue;

                // 🔁 Читаем чисельник и знаменник
                var lines = cellValue.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                var hasNumerator = lines.Length >= 2;
                var hasDenominator = lines.Length >= 4;

                var dayOfWeek = day switch
                {
                    "ПОНЕДІЛОК" => DayOfWeek.Monday,
                    "ВІВТОРОК" => DayOfWeek.Tuesday,
                    "СЕРЕДА" => DayOfWeek.Wednesday,
                    "ЧЕТВЕР" => DayOfWeek.Thursday,
                    "П'ЯТНИЦЯ" => DayOfWeek.Friday,
                    _ => DayOfWeek.Monday
                };

                if (!TryGetLessonStartTime(pairNumRaw, out TimeOnly start))
                    continue;

                var semesterStart = DateTime.Today;
                while (semesterStart.DayOfWeek != DayOfWeek.Monday)
                    semesterStart = semesterStart.AddDays(-1);

                for (int week = 0; week < 18; week++)
                {
                    bool isNumeratorWeek = week % 2 == 0;
                    string? subject = null, teacher = null;

                    if (isNumeratorWeek && hasNumerator)
                    {
                        subject = lines[0].Trim();
                        teacher = lines[1].Trim();
                    }
                    else if (!isNumeratorWeek && hasDenominator)
                    {
                        subject = lines[2].Trim();
                        teacher = lines[3].Trim();
                    }

                    if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(teacher))
                        continue;

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

        foreach (var lesson in lessons)
            await _lessonRepository.AddAsync(lesson);

        await _lessonRepository.SaveChangesAsync();
        return Ok(new { Count = lessons.Count });
    }
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
