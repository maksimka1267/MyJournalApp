using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyJournalApp.Interface; // IStudentRepository, IGroupRepository, IJournalEntryRepository, IGradeRepository, IUserRepository

[ApiController]
[Route("api/[controller]")]
[Authorize] // доступен студенту / учителю / админу
public class IndividualPlanController : ControllerBase
{
    private readonly IStudentRepository _studentRepo;
    private readonly IGroupRepository _groupRepo;
    private readonly IJournalEntryRepository _journalRepo;
    private readonly IGradeRepository _gradeRepo;
    private readonly IUserRepository _userRepo;
    private readonly IWebHostEnvironment _env;

    private const string GroupFilesFolder = "group-files";

    public IndividualPlanController(
        IStudentRepository studentRepo,
        IGroupRepository groupRepo,
        IJournalEntryRepository journalRepo,
        IGradeRepository gradeRepo,
        IUserRepository userRepo,
        IWebHostEnvironment env)
    {
        _studentRepo = studentRepo;
        _groupRepo = groupRepo;
        _journalRepo = journalRepo;
        _gradeRepo = gradeRepo;
        _userRepo = userRepo;
        _env = env;
    }

    // Для студента: его собственный план
    [HttpGet("me")]
    public async Task<IActionResult> DownloadForMe()
    {
        var me = await _userRepo.GetByIdAsync(GetUserId());
        if (me == null || !string.Equals(me.Role, "Student", StringComparison.OrdinalIgnoreCase))
            return Forbid();

        return await BuildAndReturnExcel(me.Id);
    }

    // Для админа/вчителя: по произвольному студенту
    [HttpGet("student/{studentId:guid}")]
    public async Task<IActionResult> DownloadForStudent(Guid studentId)
    {
        var me = await _userRepo.GetByIdAsync(GetUserId());
        if (me == null) return Forbid();

        var isAdminOrTeacher = string.Equals(me.Role, "Admin", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(me.Role, "Teacher", StringComparison.OrdinalIgnoreCase);
        var isSelf = me.Id == studentId;
        if (!isAdminOrTeacher && !isSelf) return Forbid();

        return await BuildAndReturnExcel(studentId);
    }

    private async Task<IActionResult> BuildAndReturnExcel(Guid studentId)
    {
        // 1) студент -> группа
        var student = await _studentRepo.GetByIdAsync(studentId);
        if (student == null || student.GroupId == Guid.Empty)
            return NotFound("Студента або його групу не знайдено.");

        var group = await _groupRepo.GetByIdAsync(student.GroupId);
        if (group == null) return NotFound("Групу не знайдено.");

        var user = await _userRepo.GetByIdAsync(studentId);
        var studentName = user?.FullName ?? "Студент";

        // 2) шаблон Excel по имени группы
        var safeGroupName = SanitizeFileName(group.Name);
        var baseDir = _env.WebRootPath ?? _env.ContentRootPath;
        var path = System.IO.Path.Combine(baseDir, GroupFilesFolder, $"{safeGroupName}.xlsx");
        if (!System.IO.File.Exists(path))
            return NotFound("Файл шаблону для цієї групи відсутній.");

        // 3) текущий семестр
        GetSemesterRange(DateTime.Today, out var semStart, out var semEnd);

        // 4) оценки студента за семестр
        var grades = await _gradeRepo.GetByStudentIdsAndDateRangeAsync(new[] { studentId }, semStart, semEnd);

        // 5) журналы группы для маппинга предметов
        var journals = await _journalRepo.GetByGroupIdAsync(group.Id); // если нет метода — заменить на GetAll + Where(GroupId)
        var subjectByJournalId = journals.ToDictionary(
            j => j.Id,
            j => ExtractSubjectFromName(j.Name, j.Subject)
        );

        // предмет -> список записей (дата, значение, тема)
        var map = new Dictionary<string, List<(DateTime dt, int? val, string? comment)>>(
            StringComparer.CurrentCultureIgnoreCase);

        foreach (var g in grades.OrderBy(x => x.Created))
        {
            if (!subjectByJournalId.TryGetValue(g.JournalEntryId, out var subj)) continue;
            if (!map.TryGetValue(subj, out var list))
            {
                list = new List<(DateTime, int?, string?)>();
                map[subj] = list;
            }
            list.Add((g.Created, g.Value, g.Comment));
        }

        // 6) открыть шаблон и заполнить
        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheets.Worksheet(1); // при необходимости — по имени листа

        // Верхний блок: заполняем по подписям строк
        SetValueByLabel(ws, "ЗДОБУВАЧ ОСВІТИ", studentName);
        SetValueByLabel(ws, "ГРУПА", group.Name);

        // найти заголовки таблицы
        var headersResult = FindHeaders(ws, new[] { "ПРЕДМЕТ", "Форма контролю", "Оцінка" });
        var headers = headersResult.Cols;
        var headerRow = headersResult.HeaderRow;

        if (!headers.TryGetValue("ПРЕДМЕТ", out var colSubject) ||
            !headers.TryGetValue("Форма контролю", out var colForm) ||
            !headers.TryGetValue("Оцінка", out var colGrade))
        {
            return BadRequest("Не вдалося знайти заголовки 'ПРЕДМЕТ' / 'Форма контролю' / 'Оцінка' у шаблоні.");
        }

        // 7) Проход по строкам: одна оценка по предмету И теме из текущей строки
        int row = headerRow + 1;
        while (true)
        {
            var subj = ws.Cell(row, colSubject).GetString().Trim();
            if (string.IsNullOrWhiteSpace(subj)) break;

            // тема (из Excel)
            var formText = ws.Cell(row, colForm).GetString().Trim();
            string gradeCellText = "-";

            if (!string.IsNullOrWhiteSpace(subj) && !string.IsNullOrWhiteSpace(formText)
                && map.TryGetValue(subj, out var list) && list.Count > 0)
            {
                var targetKey = MakeTopicKey(formText);

                // последняя по дате оценка по этой теме
                var matched = list
                    .Where(x => MakeTopicKey(x.comment) == targetKey && x.val.HasValue)
                    .OrderBy(x => x.dt)
                    .LastOrDefault();

                if (matched.val.HasValue)
                    gradeCellText = matched.val.Value.ToString();
            }

            ws.Cell(row, colGrade).Value = gradeCellText;
            row++;
        }

        // 8) отдать файл
        using var ms = new System.IO.MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;

        var downloadName = $"Індивідуальний_план_{SanitizeFileName(studentName)}.xlsx";
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            downloadName);
    }

    private Guid GetUserId()
    {
        var sub = User.Claims.FirstOrDefault(c => c.Type.EndsWith("/nameidentifier") || c.Type == "sub")?.Value;
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }

    private static string ExtractSubjectFromName(string? name, string? fallbackSubject)
    {
        var baseName = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(baseName))
            return (fallbackSubject ?? "Предмет").Trim();

        // предмет — часть ДО дефиса
        var idx = baseName.IndexOf('-');
        var beforeDash = idx >= 0 ? baseName[..idx] : baseName;
        return beforeDash.Trim();
    }

    // Приведение темы к стабильному ключу (как в JournalColumn.MakeTopicKey)
    private static string MakeTopicKey(string? topic)
        => string.IsNullOrWhiteSpace(topic)
            ? "no-topic"
            : new string(
                topic.Trim().ToLowerInvariant()
                     .Where(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')
                     .ToArray()
            );

    // Осінній: 1 Sep (Y) – 31 Jan (Y+1)
    // Весняний: 1 Feb – 30 Jun (Y)
    // Jul–Aug: считаем актуальным весенний (1 Feb – 30 Jun Y)
    private static void GetSemesterRange(DateTime today, out DateTime start, out DateTime end)
    {
        int y = today.Year;
        int m = today.Month;

        if (m >= 9) // Sep–Dec
        {
            start = new DateTime(y, 9, 1);
            end = new DateTime(y + 1, 1, 31);
        }
        else if (m == 1) // Jan — хвіст осіннього семестру
        {
            start = new DateTime(y - 1, 9, 1);
            end = new DateTime(y, 1, 31);
        }
        else if (m >= 2 && m <= 6) // Feb–Jun
        {
            start = new DateTime(y, 2, 1);
            end = new DateTime(y, 6, 30);
        }
        else // Jul–Aug
        {
            start = new DateTime(y, 2, 1);
            end = new DateTime(y, 6, 30);
        }
    }

    private sealed record HeaderFindResult(Dictionary<string, int> Cols, int HeaderRow);

    // Установить значение по метке строки (находим ячейку с текстом label; пишем в жёлтую справа или просто в соседнюю справа)
    private static void SetValueByLabel(IXLWorksheet ws, string label, string value,
                                        int maxRowsToScan = 50, int maxColsToScan = 30)
    {
        for (int r = 1; r <= maxRowsToScan; r++)
        {
            for (int c = 1; c <= maxColsToScan; c++)
            {
                var txt = (ws.Cell(r, c).GetString() ?? "").Trim();
                if (txt.Equals(label, StringComparison.CurrentCultureIgnoreCase))
                {
                    // попробовать найти жёлтую справа в этой строке
                    for (int cc = c + 1; cc <= maxColsToScan; cc++)
                    {
                        var cell = ws.Cell(r, cc);
                        var fill = cell.Style.Fill.BackgroundColor;
                        if (fill.ColorType == XLColorType.Color && IsYellowish(fill.Color))
                        {
                            cell.Value = value;
                            return;
                        }
                    }
                    // иначе — ближайшая справа
                    ws.Cell(r, c + 1).Value = value;
                    return;
                }
            }
        }
    }

    private static bool IsYellowish(System.Drawing.Color color)
        => color.R > 200 && color.G > 200 && color.B < 100;

    private static HeaderFindResult FindHeaders(IXLWorksheet ws, IEnumerable<string> names)
    {
        var wanted = names.ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        var found = new Dictionary<string, int>(StringComparer.CurrentCultureIgnoreCase);
        int headerRow = -1;

        // сканируем разумный диапазон
        for (int r = 1; r <= 50; r++)
        {
            for (int c = 1; c <= 50; c++)
            {
                var txt = ws.Cell(r, c).GetString().Trim();
                if (wanted.Contains(txt) && !found.ContainsKey(txt))
                {
                    found[txt] = c;
                    headerRow = Math.Max(headerRow, r);
                }
                if (found.Count == wanted.Count)
                {
                    return new HeaderFindResult(found, headerRow);
                }
            }
        }
        return new HeaderFindResult(found, headerRow);
    }

    private static string SanitizeFileName(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "file";
        var cleaned = Regex.Replace(input.Trim(), @"[^0-9A-Za-zА-Яа-яІіЇїЄєҐґ _\-]", "_");
        cleaned = Regex.Replace(cleaned, @"\s+", " ");
        return cleaned;
    }
}
