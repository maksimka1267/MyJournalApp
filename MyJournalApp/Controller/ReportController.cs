using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyJournalApp.Interface; // Замените на ваш namespace
using ClosedXML.Excel;
using System.IO;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

[ApiController]
[Route("api/[controller]")]
public class ReportController : ControllerBase
{
    private readonly IGroupRepository _groupRepo;
    private readonly IUserRepository _userRepo;
    private readonly IGradeRepository _gradeRepo;

    public ReportController(
        IGroupRepository groupRepo,
        IUserRepository userRepo,
        IGradeRepository gradeRepo)
    {
        _groupRepo = groupRepo;
        _userRepo = userRepo;
        _gradeRepo = gradeRepo;
    }

    [Authorize]
    [HttpGet("absences/group/{groupId}")]
    public async Task<IActionResult> GenerateAbsenceReport(
    Guid groupId,
    [FromQuery] DateTime startDate,
    [FromQuery] DateTime endDate)
    {
        // Перевірка групи
        var group = await _groupRepo.GetByIdAsync(groupId);
        if (group == null || group.StudentIds == null || !group.StudentIds.Any())
            return NotFound("Групу не знайдено або в ній немає студентів.");

        // Отримуємо студентів
        var students = await _userRepo.GetUsersByIdsAsync(group.StudentIds);
        var studentDict = students.ToDictionary(s => s.Id, s => s.FullName);

        // Отримуємо відсутності за вказаний період
        var absences = await _gradeRepo.GetAbsencesByStudentIdsAndDateRangeAsync(group.StudentIds, startDate, endDate);

        // Групуємо по студенту
        var studentAbsences = absences
            .GroupBy(a => a.StudentId)
            .ToDictionary(
                g => g.Key,
                g => new HashSet<DateTime>(g.Select(a => a.Created.Date))
            );

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add($"{group.Name} - Рапортичка");

        // --- Будні дати ---
        var dates = Enumerable.Range(0, (endDate - startDate).Days + 1)
            .Select(i => startDate.AddDays(i))
            .Where(d => d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
            .ToList();

        int colCount = 2 + dates.Count;

        // --- Заголовок ---
        ws.Cell(1, 1).Value = $"Рапортичка відвідування групи {group.Name}";
        ws.Range(1, 1, 1, colCount).Merge().Style
            .Font.SetBold().Font.FontSize = 14;
        ws.Row(1).Height = 25;
        ws.Row(1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Row(1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        // --- Шапка ---
        ws.Cell(2, 1).Value = "№";
        ws.Cell(2, 2).Value = "ПІБ студента";

        for (int i = 0; i < dates.Count; i++)
        {
            ws.Cell(2, 3 + i).Value = dates[i].ToString("dd.MM");
        }

        ws.Range(2, 1, 2, colCount).Style
            .Font.SetBold()
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center);

        // --- Заповнення студентів ---
        int row = 3;
        int number = 1;
        foreach (var student in studentDict.Values.OrderBy(n => n))
        {
            ws.Cell(row, 1).Value = number++;
            ws.Cell(row, 2).Value = student;

            for (int i = 0; i < dates.Count; i++)
            {
                var date = dates[i];
                var studentId = studentDict.First(s => s.Value == student).Key;

                if (studentAbsences.TryGetValue(studentId, out var absenceDates) &&
                    absenceDates.Contains(date))
                {
                    ws.Cell(row, 3 + i).Value = "Н";
                    ws.Cell(row, 3 + i).Style.Font.FontColor = XLColor.Red;
                    ws.Cell(row, 3 + i).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }
            }

            row++;
        }

        // --- Автоматичне підлаштування ширини ---
        ws.Columns().AdjustToContents();
        ws.Range(1, 1, row - 1, colCount).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        ws.Range(1, 1, row - 1, colCount).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Seek(0, SeekOrigin.Begin);

        var fileName = $"Рапортичка_{group.Name}.xlsx";
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}
