using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyJournalApp.Interface; // твой namespace c репами
using System.Globalization;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StudentGradesReportController : ControllerBase
{
    private readonly IGradeRepository _gradeRepo;
    private readonly IUserRepository _userRepo;
    private readonly IJournalEntryRepository _journalRepo;

    public StudentGradesReportController(
        IGradeRepository gradeRepo,
        IUserRepository userRepo,
        IJournalEntryRepository journalRepo)
    {
        _gradeRepo = gradeRepo;
        _userRepo = userRepo;
        _journalRepo = journalRepo;
    }

    public class StudentGradesReportDto
    {
        public Guid StudentId { get; set; }
        public string StudentName { get; set; } = "";
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<DateTime> Dates { get; set; } = new();

        // Строки таблицы — по предметам (журналам)
        public List<SubjectRow> Rows { get; set; } = new();

        public class SubjectRow
        {
            public string SubjectName { get; set; } = "";

            // Все оценки за конкретную дату: ключ = yyyyMMdd, значение = список оценок по порядку времени
            // null-оценки (Value == null) игнорируются; 0 остаётся как 0
            public Dictionary<string, List<int>> Cells { get; set; } = new();
        }
    }

    // ---------- JSON ----------
    // GET /api/StudentGradesReport/student-grades?studentId=...&start=YYYY-MM-DD&end=YYYY-MM-DD
    [HttpGet("student-grades")]
    public async Task<IActionResult> GetStudentGrades(
        [FromQuery] Guid studentId,
        [FromQuery] DateTime start,
        [FromQuery] DateTime end)
    {
        if (studentId == Guid.Empty || start == default || end == default || start > end)
            return BadRequest("Invalid studentId/start/end");

        var student = await _userRepo.GetByIdAsync(studentId);
        if (student == null) return NotFound("Студента не знайдено.");

        // Все дни диапазона (включая выходные)
        var dates = Enumerable.Range(0, (end.Date - start.Date).Days + 1)
                              .Select(i => start.Date.AddDays(i))
                              .ToList();

        // Все записи оценок студента за период
        var grades = await _gradeRepo.GetByStudentIdsAndDateRangeAsync(new[] { studentId }, start, end);

        // Предметы по журналам
        var journalIds = grades.Select(g => g.JournalEntryId).Distinct().ToList();
        var subjectByJournal = new Dictionary<Guid, string>();
        foreach (var jid in journalIds)
        {
            var j = await _journalRepo.GetByIdAsync(jid);
            if (j != null)
                subjectByJournal[jid] = string.IsNullOrWhiteSpace(j.Name) ? (j.Subject ?? "Предмет") : j.Name;
        }

        // Строки: по предметам (журналам)
        var rows = grades
            .GroupBy(g => g.JournalEntryId)
            .Select(gr =>
            {
                var subject = subjectByJournal.GetValueOrDefault(gr.Key, "Предмет");

                // База по всем датам: пустые списки
                var map = dates.ToDictionary(d => d.ToString("yyyyMMdd"), _ => new List<int>());

                // На каждую дату — собираем ВСЕ оценки (Value), сохраняя 0, игнорируя null
                foreach (var day in gr.GroupBy(x => x.Created.Date).OrderBy(g => g.Key))
                {
                    var list = day
                        .OrderBy(x => x.Created)            // хронологически
                        .Select(x => x.Value)               // int?
                        .Where(v => v.HasValue)             // без null
                        .Select(v => v!.Value)              // int
                        .ToList();

                    if (list.Count > 0)
                        map[day.Key.ToString("yyyyMMdd")] = list; // может быть 1,2,3 и т.д.
                }

                return new StudentGradesReportDto.SubjectRow
                {
                    SubjectName = subject,
                    Cells = map
                };
            })
            .OrderBy(r => r.SubjectName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var dto = new StudentGradesReportDto
        {
            StudentId = studentId,
            StudentName = student.FullName,
            StartDate = start.Date,
            EndDate = end.Date,
            Dates = dates,
            Rows = rows
        };

        return Ok(dto);
    }

    // ---------- EXCEL ----------
    // GET /api/StudentGradesReport/student-grades/export?studentId=...&start=YYYY-MM-DD&end=YYYY-MM-DD
    [HttpGet("student-grades/export")]
    public async Task<IActionResult> ExportStudentGradesExcel(
        [FromQuery] Guid studentId,
        [FromQuery] DateTime start,
        [FromQuery] DateTime end)
    {
        var result = await GetStudentGrades(studentId, start, end) as OkObjectResult;
        if (result?.Value is not StudentGradesReportDto dto)
            return BadRequest("Неможливо побудувати звіт.");

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Рапортичка оцінок");

        // Для каждой даты узнаём «максимальное количество оценок» среди всех предметов — это ширина под-даты
        var multiplicityByDate = dto.Dates.ToDictionary(d => d, d => 1); // минимум одна колонка
        foreach (var d in dto.Dates)
        {
            var key = d.ToString("yyyyMMdd");
            var maxK = dto.Rows
                .Select(r => r.Cells.TryGetValue(key, out var list) ? list.Count : 0)
                .DefaultIfEmpty(0)
                .Max();

            multiplicityByDate[d] = Math.Max(1, maxK);
        }

        // Подсчитываем общее число колонок: 2 фиксированных (№, Предмет) + сумма подколонок по датам
        var totalDataCols = multiplicityByDate.Values.Sum();
        var colCount = 2 + totalDataCols;

        // Заголовок (ряд 1)
        ws.Cell(1, 1).Value = $"Рапортичка оцінок студента {dto.StudentName}";
        ws.Range(1, 1, 1, colCount).Merge().Style.Font.SetBold().Font.FontSize = 14;
        ws.Row(1).Height = 24;
        ws.Row(1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Шапка: двухъярусная
        // Ряд 2: "№" | "Предмет" | <дата (мердж на K ячеек)> ...
        // Ряд 3:                               1 | 2 | ... | K
        ws.Cell(2, 1).Value = "№";
        ws.Cell(2, 2).Value = "Предмет";
        ws.Range(2, 1, 3, 1).Merge(); // "№" на 2 строки
        ws.Range(2, 2, 3, 2).Merge(); // "Предмет" на 2 строки

        int curCol = 3;
        foreach (var d in dto.Dates)
        {
            int k = multiplicityByDate[d];
            if (k == 1)
            {
                ws.Cell(2, curCol).Value = d.ToString("dd.MM");
                ws.Range(2, curCol, 3, curCol).Merge(); // одна колонка на две строки
                curCol++;
            }
            else
            {
                // Мержим заголовок даты на K столбцов
                ws.Range(2, curCol, 2, curCol + k - 1).Merge().Value = d.ToString("dd.MM");
                // Нумерация подколонок в ряду 3: 1..K
                for (int i = 0; i < k; i++)
                    ws.Cell(3, curCol + i).Value = (i + 1);
                curCol += k;
            }
        }

        ws.Range(2, 1, 3, colCount).Style
            .Font.SetBold()
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center);

        // Данные, начиная с 4-й строки
        int row = 4; int num = 1;
        foreach (var r in dto.Rows)
        {
            ws.Cell(row, 1).Value = num++;
            ws.Cell(row, 2).Value = r.SubjectName;

            curCol = 3;
            foreach (var d in dto.Dates)
            {
                var key = d.ToString("yyyyMMdd");
                var k = multiplicityByDate[d];
                r.Cells.TryGetValue(key, out var list);
                list ??= new List<int>();

                for (int i = 0; i < k; i++)
                {
                    var cell = ws.Cell(row, curCol + i);
                    if (i < list.Count)
                    {
                        cell.Value = list[i];
                    }
                    else
                    {
                        cell.Value = "–";
                        cell.Style.Font.FontColor = XLColor.Gray;
                    }
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }
                curCol += k;
            }

            row++;
        }

        // Оформление
        ws.Columns().AdjustToContents();
        ws.Range(1, 1, row - 1, colCount).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        ws.Range(1, 1, row - 1, colCount).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        // Выгрузка
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;

        // имя файла по твоему требованию
        var fileName = $"файл з оцінками студента {SanitizeFileName(dto.StudentName)}.xlsx";
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
    }
}
