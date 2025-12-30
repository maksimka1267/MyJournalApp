using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyJournalApp.Interface;
using ClosedXML.Excel;
using System.IO;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;

[ApiController]
[Route("api/[controller]")]
public class ReportController : ControllerBase
{
    private readonly IGroupRepository _groupRepo;
    private readonly IUserRepository _userRepo;
    private readonly IGradeRepository _gradeRepo;
    private readonly IJournalEntryRepository _journalRepo;

    public ReportController(
        IGroupRepository groupRepo,
        IUserRepository userRepo,
        IGradeRepository gradeRepo,
        IJournalEntryRepository journalRepo)
    {
        _groupRepo = groupRepo;
        _userRepo = userRepo;
        _gradeRepo = gradeRepo;
        _journalRepo = journalRepo;
    }

    [Authorize]
    [HttpGet("absences/group/{groupId}")]
    public async Task<IActionResult> GenerateAbsenceReport(
        Guid groupId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        var group = await _groupRepo.GetByIdAsync(groupId);
        if (group == null || group.StudentIds == null || !group.StudentIds.Any())
            return NotFound("Групу не знайдено або в ній немає студентів.");

        // Студенти (Id -> ПІБ), відсортовані за ПІБ
        var students = await _userRepo.GetUsersByIdsAsync(group.StudentIds);
        var orderedStudents = students
            .Select(s => new { s.Id, Name = s.FullName })
            .OrderBy(s => s.Name)
            .ToList();

        // Усі «Н» за період
        var absences = await _gradeRepo.GetAbsencesByStudentIdsAndDateRangeAsync(
            group.StudentIds, startDate, endDate);

        // Журнали групи: JournalId -> предмет (людська назва)
        var journals = await _journalRepo.GetByGroupIdAsync(group.Id) ?? new List<JournalEntry>();
        var subjectByJournalId = journals.ToDictionary(
            j => j.Id,
            j => ExtractSubjectFromName(j.Name, j.Subject)
        );

        // Робочі дати (без вихідних)
        var dates = Enumerable.Range(0, (endDate.Date - startDate.Date).Days + 1)
            .Select(i => startDate.Date.AddDays(i))
            .Where(d => d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
            .ToList();

        // Для кожної дати — унікальні предмети (за якими були «Н» по всій групі) у порядку частоти
        // Якщо за день не було «Н», все одно залишимо 1 підколонку (без підпису)
        var dateSubjects = new Dictionary<DateTime, List<string>>();
        var dateSubjectKeys = new Dictionary<DateTime, List<string>>(); // нормалізовані (UPPER) для пошуку

        foreach (var d in dates)
        {
            var daySubjects = absences
                .Where(a => a.Created.Date == d && subjectByJournalId.ContainsKey(a.JournalEntryId))
                .Select(a => subjectByJournalId[a.JournalEntryId])
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .GroupBy(s => s, StringComparer.CurrentCultureIgnoreCase)
                .Select(g => new { Subject = g.Key, Cnt = g.Count() })
                .OrderByDescending(x => x.Cnt)
                .ThenBy(x => x.Subject)
                .Select(x => x.Subject)
                .ToList();

            if (daySubjects.Count == 0)
                daySubjects.Add(string.Empty); // одна "порожня" підколонка, предметів немає

            dateSubjects[d] = daySubjects;
            dateSubjectKeys[d] = daySubjects
                .Select(s => (s ?? string.Empty).Trim().ToUpperInvariant())
                .ToList();
        }

        // Підрахунок колонок: 2 службові + сума підколонок по всіх датах
        int totalDateSubcols = dateSubjects.Values.Sum(list => list.Count);
        int colCount = 2 + totalDateSubcols;

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add($"{group.Name} - Рапортичка");

        // ----- Заголовок -----
        ws.Cell(1, 1).Value = $"Рапортичка відвідування групи {group.Name}";
        ws.Range(1, 1, 1, colCount).Merge().Style
            .Font.SetBold().Font.SetFontSize(14);
        ws.Row(1).Height = 25;
        ws.Row(1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Row(1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        // ----- Шапка (ряди 2-3) -----
        ws.Cell(2, 1).Value = "№";
        ws.Cell(2, 2).Value = "ПІБ студента";
        ws.Range(2, 1, 3, 1).Merge();
        ws.Range(2, 2, 3, 2).Merge();
        ws.Range(2, 1, 3, 2).Style
            .Font.SetBold()
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center);

        // Шапка по датах
        int currentCol = 3;
        foreach (var d in dates)
        {
            var subjects = dateSubjects[d];
            int slots = subjects.Count;

            // Верхня комірка з датою
            ws.Cell(2, currentCol).Value = d.ToString("dd.MM");
            if (slots > 1)
                ws.Range(2, currentCol, 2, currentCol + slots - 1).Merge();

            // Ряд 3 завжди містить назви предметів (якщо предметів немає — заголовок порожній)
            for (int j = 0; j < slots; j++)
                ws.Cell(3, currentCol + j).Value = subjects[j];

            currentCol += slots;
        }

        ws.Range(2, 3, 3, colCount).Style
            .Font.SetBold()
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center);

        // Стартова колонка для кожного дня
        var dateStartCol = new Dictionary<DateTime, int>();
        currentCol = 3;
        foreach (var d in dates)
        {
            dateStartCol[d] = currentCol;
            currentCol += dateSubjects[d].Count;
        }

        // (studentId, day, subjectKey) -> count N   (subjectKey = UPPERCASE)
        var nByStudentDaySubject = absences
            .Where(a => subjectByJournalId.TryGetValue(a.JournalEntryId, out var subjName) && !string.IsNullOrWhiteSpace(subjName))
            .Select(a => (
                a.StudentId,
                Day: a.Created.Date,
                SubjectKey: subjectByJournalId[a.JournalEntryId].Trim().ToUpperInvariant()
            ))
            .GroupBy(x => (x.StudentId, x.Day, x.SubjectKey))
            .ToDictionary(g => g.Key, g => g.Count());

        // ----- Дані -----
        int row = 4;
        int number = 1;

        foreach (var s in orderedStudents)
        {
            ws.Cell(row, 1).Value = number++;
            ws.Cell(row, 2).Value = s.Name;

            foreach (var d in dates)
            {
                var startCol = dateStartCol[d];
                var subjects = dateSubjects[d];
                var subjectsKeys = dateSubjectKeys[d];

                // Для кожної підколонки (предмета) ставимо Н або Н×k
                for (int j = 0; j < subjects.Count; j++)
                {
                    var subjKey = subjectsKeys[j];

                    // Якщо subjKey пустий (тобто за день не було відсутностей) — пропускаємо
                    if (string.IsNullOrEmpty(subjKey)) continue;

                    nByStudentDaySubject.TryGetValue((s.Id, d, subjKey), out var cnt);
                    if (cnt > 0)
                    {
                        var cell = ws.Cell(row, startCol + j);
                        cell.Value = cnt == 1 ? "Н" : $"Н×{cnt}";
                        cell.Style.Font.FontColor = XLColor.Red;
                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    }
                }
            }

            row++;
        }

        // ----- Стилі/границі -----
        ws.Columns().AdjustToContents();
        ws.Range(1, 1, row - 1, colCount).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        ws.Range(1, 1, row - 1, colCount).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var fileName = $"Рапортичка_{SanitizeFileName(group.Name)}.xlsx";
        return File(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    // -------- helpers --------

    private static string ExtractSubjectFromName(string? name, string? fallbackSubject)
    {
        var baseName = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(baseName))
            return (fallbackSubject ?? "Предмет").Trim();

        var idx = baseName.IndexOf('-');
        var beforeDash = idx >= 0 ? baseName[..idx] : baseName;
        return beforeDash.Trim();
    }

    private static string SanitizeFileName(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "file";
        var cleaned = Regex.Replace(input.Trim(), @"[^0-9A-Za-zА-Яа-яІіЇїЄєҐґ _\-]", "_");
        cleaned = Regex.Replace(cleaned, @"\s+", " ");
        return cleaned;
    }
}
