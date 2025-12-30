using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyJournalApp.Interface; // IJournalEntryRepository, IGradeRepository, IGroupRepository, IUserRepository

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class JournalExportController : ControllerBase
{
    private readonly IJournalEntryRepository _journalRepo;
    private readonly IGradeRepository _gradeRepo;
    private readonly IGroupRepository _groupRepo;
    private readonly IUserRepository _userRepo;

    public JournalExportController(
        IJournalEntryRepository journalRepo,
        IGradeRepository gradeRepo,
        IGroupRepository groupRepo,
        IUserRepository userRepo)
    {
        _journalRepo = journalRepo;
        _gradeRepo = gradeRepo;
        _groupRepo = groupRepo;
        _userRepo = userRepo;
    }

    [HttpGet("{journalId:guid}")]
    public async Task<IActionResult> Export(Guid journalId)
    {
        // 1) Журнал
        var journal = await _journalRepo.GetByIdAsync(journalId);
        if (journal == null)
            return NotFound("Журнал не знайдено.");

        // 2) Студенты группы
        var group = await _groupRepo.GetByIdAsync(journal.GroupId);
        if (group?.StudentIds == null || group.StudentIds.Count == 0)
            return BadRequest("У групі немає студентів.");

        var students = await _userRepo.GetUsersByIdsAsync(group.StudentIds);
        var studentsOrdered = students.OrderBy(s => s.FullName).ToList();

        // 3) Все оценки журнала
        var grades = (await _gradeRepo.GetByJournalEntryIdAsync(journalId)).ToList();

        // Берём записи, где есть либо оценка, либо отметка посещаемости
        grades = grades
            .Where(g => g.Value.HasValue || g.IsPresent.HasValue)
            .ToList();

        // 4) Колонки: (дата, тема). Допускаем несколько колонок в один день с разными темами.
        // Группируем по Date + normalized TopicKey и упорядочиваем: дата ↑, затем время создания ↑, затем тема.
        var cols = grades
            .GroupBy(g => new { D = g.Created.Date, TopicKey = MakeTopicKey(g.Comment) })
            .Select(gr => new
            {
                Date = gr.Key.D,
                Topic = gr.Select(x => x.Comment).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)) ?? "",
                FirstCreated = gr.Min(x => x.Created)
            })
            .OrderBy(x => x.Date)
            .ThenBy(x => x.FirstCreated)
            .ThenBy(x => x.Topic)
            .ToList();

        // 5) Билдим словарь для быстрого поиска ячеек: (studentId, date, topicKey) -> последняя запись
        var cellMap = grades
            .GroupBy(g => new { g.StudentId, D = g.Created.Date, TopicKey = MakeTopicKey(g.Comment) })
            .ToDictionary(
                gr => (gr.Key.StudentId, gr.Key.D, gr.Key.TopicKey),
                gr => gr.OrderBy(x => x.Created).Last() // если вдруг дубль — берём последнюю по времени
            );

        // 6) Генерация Excel
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Журнал");

        // Заголовок
        ws.Cell(1, 1).Value = SanitizeTitle(journal.Name);
        ws.Range(1, 1, 1, 2 + cols.Count).Merge()
            .Style.Font.SetBold().Font.SetFontSize(14);
        ws.Row(1).Height = 24;
        ws.Row(1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Шапка
        ws.Cell(2, 1).Value = "№";
        ws.Cell(2, 2).Value = "ПІБ студента";

        for (int i = 0; i < cols.Count; i++)
        {
            var c = cols[i];
            // В шапке выводим тему + дату (где дата ще указана тема)
            ws.Cell(2, 3 + i).Value = $"{(string.IsNullOrWhiteSpace(c.Topic) ? "—" : c.Topic)}\n{c.Date:dd.MM}";
            ws.Cell(2, 3 + i).Style.Alignment.WrapText = true;
        }

        ws.Range(2, 1, 2, 2 + cols.Count).Style
            .Font.SetBold()
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center);

        // Данные
        int row = 3;
        int idx = 1;
        foreach (var s in studentsOrdered)
        {
            ws.Cell(row, 1).Value = idx++;
            ws.Cell(row, 2).Value = s.FullName;

            for (int i = 0; i < cols.Count; i++)
            {
                var c = cols[i];
                var key = (s.Id, c.Date, MakeTopicKey(c.Topic));

                if (cellMap.TryGetValue(key, out var g) && (g.Value.HasValue || g.IsPresent.HasValue))
                {
                    string gradeText = g.Value.HasValue ? g.Value.Value.ToString() : string.Empty;
                    string presenceText = g.IsPresent.HasValue
                        ? (g.IsPresent.Value ? "П" : "Н")
                        : string.Empty;

                    string cellText;

                    if (!string.IsNullOrEmpty(gradeText) && !string.IsNullOrEmpty(presenceText))
                    {
                        // пример: "10 (П)" или "8 (Н)"
                        cellText = $"{gradeText} ({presenceText})";
                    }
                    else if (!string.IsNullOrEmpty(gradeText))
                    {
                        cellText = gradeText; // только оценка
                    }
                    else if (!string.IsNullOrEmpty(presenceText))
                    {
                        cellText = presenceText; // только присутствие/отсутствие
                    }
                    else
                    {
                        cellText = "–";
                    }

                    ws.Cell(row, 3 + i).Value = cellText;
                    ws.Cell(row, 3 + i).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }
                else
                {
                    ws.Cell(row, 3 + i).Value = "–";
                    ws.Cell(row, 3 + i).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    ws.Cell(row, 3 + i).Style.Font.FontColor = XLColor.Gray;
                }
            }

            row++;
        }

        // Легенда по посещаемости (по желанию — можно убрать)
        ws.Cell(row + 1, 1).Value = "П — присутній, Н — відсутній";
        ws.Range(row + 1, 1, row + 1, 3).Merge();
        ws.Row(row + 1).Style.Font.Italic = true;

        // Оформление
        ws.Columns().AdjustToContents();
        ws.Column(2).Width = Math.Max(ws.Column(2).Width, 28); // ПІБ пошире
        ws.Range(1, 1, row - 1, 2 + cols.Count).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        ws.Range(1, 1, row - 1, 2 + cols.Count).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        // 7) Отдаём файл. Имя файла = название журнала.
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;

        var fileName = $"{SanitizeFileName(journal.Name)}.xlsx";
        return File(
            ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName
        );
    }

    private static string MakeTopicKey(string? topic)
        => string.IsNullOrWhiteSpace(topic)
            ? "no-topic"
            : new string(topic.Trim().ToLowerInvariant()
                .Where(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')
                .ToArray());

    private static string SanitizeFileName(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "journal";
        var cleaned = Regex.Replace(input.Trim(), @"[^0-9A-Za-zА-Яа-яІіЇїЄєҐґ _\-()\.]", "_");
        cleaned = Regex.Replace(cleaned, @"\s+", " ");
        return cleaned;
    }

    private static string SanitizeTitle(string? name)
        => string.IsNullOrWhiteSpace(name) ? "Журнал" : name.Trim();
}
