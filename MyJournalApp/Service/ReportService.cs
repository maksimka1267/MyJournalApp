using ClosedXML.Excel;
using MyJournalApp.Data.Dtos.Absence;
using MyJournalApp.Interface;
using MyJournalApp.Service.Interface;
using System.Text.RegularExpressions;

namespace MyJournalApp.Service
{
    public class ReportService : IReportService
    {
        private readonly IGroupRepository _groupRepository;
        private readonly IUserRepository _userRepository;
        private readonly IGradeRepository _gradeRepository;
        private readonly IJournalEntryRepository _journalRepository;

        public ReportService(
            IGroupRepository groupRepository,
            IUserRepository userRepository,
            IGradeRepository gradeRepository,
            IJournalEntryRepository journalRepository)
        {
            _groupRepository = groupRepository;
            _userRepository = userRepository;
            _gradeRepository = gradeRepository;
            _journalRepository = journalRepository;
        }

        public async Task<ReportFileDto> GenerateAbsenceReportAsync(AbsenceReportDto dto)
        {
            var group = await LoadGroupAsync(dto);

            var students = await LoadStudentsAsync(group);

            var absences = await LoadAbsencesAsync(group, dto);

            var subjectDictionary = await BuildSubjectDictionaryAsync(group);

            var workbook = BuildWorkbook(
                group,
                students,
                absences,
                subjectDictionary,
                dto);

            return CreateResult(workbook, group.Name);
        }
        private async Task<Group> LoadGroupAsync(AbsenceReportDto dto)
        {
            var group = await _groupRepository.GetByIdAsync(dto.GroupId);

            if (group == null ||
                group.StudentIds == null ||
                !group.StudentIds.Any())
            {
                throw new ArgumentException(
                    "Групу не знайдено або в ній немає студентів.");
            }

            return group;
        }
        private async Task<List<(Guid Id, string Name)>> LoadStudentsAsync(Group group)
        {
            var students = await _userRepository.GetUsersByIdsAsync(group.StudentIds);

            return students
                .Select(s => (s.Id, s.FullName))
                .OrderBy(s => s.FullName)
                .ToList();
        }
        private async Task<List<Grade>> LoadAbsencesAsync(Group group,AbsenceReportDto dto)
        {
            var absences =
                await _gradeRepository.GetAbsencesByStudentIdsAndDateRangeAsync(
                    group.StudentIds,
                    dto.StartDate,
                    dto.EndDate);

            return absences.ToList();
        }
        private async Task<Dictionary<Guid, string>> BuildSubjectDictionaryAsync(Group group)
        {
            var journals =
                await _journalRepository.GetByGroupIdAsync(group.Id)
                ?? new List<JournalEntry>();

            return journals.ToDictionary(
                j => j.Id,
                j => ExtractSubjectFromName(
                    j.Name,
                    j.Subject));
        }
        private XLWorkbook BuildWorkbook(
    Group group,
    List<(Guid Id, string Name)> students,
    List<Grade> absences,
    Dictionary<Guid, string> subjectByJournalId,
    AbsenceReportDto dto)
        {
            // Рабочие даты (без выходных)
            var dates = Enumerable.Range(
                    0,
                    (dto.EndDate.Date - dto.StartDate.Date).Days + 1)
                .Select(i => dto.StartDate.Date.AddDays(i))
                .Where(d =>
                    d.DayOfWeek != DayOfWeek.Saturday &&
                    d.DayOfWeek != DayOfWeek.Sunday)
                .ToList();

            // Для кожної дати формуємо список предметів
            var dateSubjects = new Dictionary<DateTime, List<string>>();
            var dateSubjectKeys = new Dictionary<DateTime, List<string>>();

            foreach (var date in dates)
            {
                var subjects = absences
                    .Where(a =>
                        a.Created.Date == date &&
                        subjectByJournalId.ContainsKey(a.JournalEntryId))
                    .Select(a => subjectByJournalId[a.JournalEntryId])
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .GroupBy(s => s, StringComparer.CurrentCultureIgnoreCase)
                    .Select(g => new
                    {
                        Subject = g.Key,
                        Count = g.Count()
                    })
                    .OrderByDescending(x => x.Count)
                    .ThenBy(x => x.Subject)
                    .Select(x => x.Subject)
                    .ToList();

                if (subjects.Count == 0)
                    subjects.Add(string.Empty);

                dateSubjects[date] = subjects;

                dateSubjectKeys[date] = subjects
                    .Select(x => (x ?? string.Empty)
                        .Trim()
                        .ToUpperInvariant())
                    .ToList();
            }

            int totalDateColumns = dateSubjects.Sum(x => x.Value.Count);
            int totalColumns = totalDateColumns + 2;

            var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add($"{group.Name} - Рапортичка");

            // Заголовок
            worksheet.Cell(1, 1).Value =
                $"Рапортичка відвідування групи {group.Name}";

            worksheet.Range(1, 1, 1, totalColumns)
                .Merge()
                .Style.Font.SetBold()
                .Font.SetFontSize(14);

            worksheet.Row(1).Height = 25;
            worksheet.Row(1).Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;
            worksheet.Row(1).Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center;

            // Шапка
            worksheet.Cell(2, 1).Value = "№";
            worksheet.Cell(2, 2).Value = "ПІБ студента";

            worksheet.Range(2, 1, 3, 1).Merge();
            worksheet.Range(2, 2, 3, 2).Merge();

            worksheet.Range(2, 1, 3, 2)
                .Style.Font.SetBold()
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                .Alignment.SetVertical(XLAlignmentVerticalValues.Center);

            int currentColumn = 3;

            foreach (var date in dates)
            {
                var subjects = dateSubjects[date];

                worksheet.Cell(2, currentColumn).Value =
                    date.ToString("dd.MM");

                if (subjects.Count > 1)
                {
                    worksheet.Range(
                        2,
                        currentColumn,
                        2,
                        currentColumn + subjects.Count - 1)
                        .Merge();
                }

                for (int i = 0; i < subjects.Count; i++)
                {
                    worksheet.Cell(3, currentColumn + i).Value =
                        subjects[i];
                }

                currentColumn += subjects.Count;
            }

            worksheet.Range(2, 3, 3, totalColumns)
                .Style.Font.SetBold()
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                .Alignment.SetVertical(XLAlignmentVerticalValues.Center);

            // Стартові колонки кожної дати
            var dateStartColumns = new Dictionary<DateTime, int>();

            currentColumn = 3;

            foreach (var date in dates)
            {
                dateStartColumns[date] = currentColumn;
                currentColumn += dateSubjects[date].Count;
            }

            // (StudentId, Date, Subject) -> Count
            var absencesMap = absences
                .Where(a =>
                    subjectByJournalId.TryGetValue(
                        a.JournalEntryId,
                        out var subject) &&
                    !string.IsNullOrWhiteSpace(subject))
                .Select(a => (
                    a.StudentId,
                    Day: a.Created.Date,
                    Subject:
                        subjectByJournalId[a.JournalEntryId]
                            .Trim()
                            .ToUpperInvariant()))
                .GroupBy(x => (x.StudentId, x.Day, x.Subject))
                .ToDictionary(
                    g => g.Key,
                    g => g.Count());

            // Дані
            int row = 4;
            int number = 1;

            foreach (var student in students)
            {
                worksheet.Cell(row, 1).Value = number++;
                worksheet.Cell(row, 2).Value = student.Name;

                foreach (var date in dates)
                {
                    int startColumn = dateStartColumns[date];

                    var subjects = dateSubjects[date];
                    var keys = dateSubjectKeys[date];

                    for (int i = 0; i < subjects.Count; i++)
                    {
                        if (string.IsNullOrWhiteSpace(keys[i]))
                            continue;

                        absencesMap.TryGetValue(
                            (student.Id, date, keys[i]),
                            out int count);

                        if (count == 0)
                            continue;

                        var cell = worksheet.Cell(row, startColumn + i);

                        cell.Value = count == 1
                            ? "Н"
                            : $"Н×{count}";

                        cell.Style.Font.FontColor = XLColor.Red;
                        cell.Style.Alignment.Horizontal =
                            XLAlignmentHorizontalValues.Center;
                        cell.Style.Alignment.Vertical =
                            XLAlignmentVerticalValues.Center;
                    }
                }

                row++;
            }

            worksheet.Columns().AdjustToContents();

            worksheet.Range(1, 1, row - 1, totalColumns)
                .Style.Border.OutsideBorder =
                XLBorderStyleValues.Thin;

            worksheet.Range(1, 1, row - 1, totalColumns)
                .Style.Border.InsideBorder =
                XLBorderStyleValues.Thin;

            return workbook;
        }
        private ReportFileDto CreateResult(XLWorkbook workbook,string groupName)
        {
            using var stream = new MemoryStream();

            workbook.SaveAs(stream);

            return new ReportFileDto
            {
                Content = stream.ToArray(),
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                FileName = $"Рапортичка_{SanitizeFileName(groupName)}.xlsx"
            };
        }
        private static string ExtractSubjectFromName(string? name,string? fallbackSubject)
        {
            var baseName = (name ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(baseName))
                return (fallbackSubject ?? "Предмет").Trim();

            var index = baseName.IndexOf('-');

            var subject = index >= 0
                ? baseName[..index]
                : baseName;

            return subject.Trim();
        }
        private static string SanitizeFileName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "file";

            var cleaned = Regex.Replace(
                input.Trim(),
                @"[^0-9A-Za-zА-Яа-яІіЇїЄєҐґ _\-]",
                "_");

            cleaned = Regex.Replace(cleaned, @"\s+", " ");

            return cleaned;
        }
    }
}