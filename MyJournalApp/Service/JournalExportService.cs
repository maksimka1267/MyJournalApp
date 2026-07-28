using ClosedXML.Excel;
using MyJournalApp.Data.Dtos.Journal;
using MyJournalApp.Helpers;
using MyJournalApp.Interface;
using MyJournalApp.Result;
using MyJournalApp.Service.Interface;
using System.Text.RegularExpressions;

namespace MyJournalApp.Service
{
    public class JournalExportService : IJournalExportService
    {
        private readonly IJournalEntryRepository _journalRepository;
        private readonly IGradeRepository _gradeRepository;
        private readonly IGroupRepository _groupRepository;
        private readonly IUserRepository _userRepository;

        public JournalExportService(
            IJournalEntryRepository journalRepository,
            IGradeRepository gradeRepository,
            IGroupRepository groupRepository,
            IUserRepository userRepository)
        {
            _journalRepository = journalRepository;
            _gradeRepository = gradeRepository;
            _groupRepository = groupRepository;
            _userRepository = userRepository;
        }
        public async Task<ServiceResult<JournalExportDto>> ExportAsync(Guid journalId)
        {
            var journal = await LoadJournalAsync(journalId);

            if (journal == null)
                return ServiceResult<JournalExportDto>.Fail("Журнал не знайдено.");

            var students = await LoadStudentsAsync(journal);

            if (!students.Any())
                return ServiceResult<JournalExportDto>.Fail("У групі немає студентів.");

            var grades = await LoadGradesAsync(journalId);

            var columns = BuildColumns(grades);

            var cellMap = BuildCellMap(grades);

            using var workbook = await CreateWorkbook(
                journal,
                students,
                columns,
                cellMap);

            return ServiceResult<JournalExportDto>.Ok(
                BuildResult(workbook, journal.Name));
        }
        public async Task<ServiceResult<List<JournalExportItemDto>>> GetJournalsAsync(
    ExportSemesterRequestDto dto)
        {
            if (dto.Semester != 1 && dto.Semester != 2)
                return ServiceResult<List<JournalExportItemDto>>
                    .Fail("Семестр має бути 1 або 2.");

            var period = SemesterHelper.GetPeriod(dto.Year, dto.Semester);

            var journals = await _journalRepository.GetByPeriodAsync(
                period.Start,
                period.End);
            var groups = await LoadGroupsAsync(journals);

            var teachers = await LoadTeachersAsync(journals);

            var result = BuildItems(
                journals,
                groups,
                teachers);

            return ServiceResult<List<JournalExportItemDto>>
                .Ok(result);
        }
        public async Task<ServiceResult<JournalExportDto>> ExportSemesterAsync(
    ExportSemesterRequestDto dto)
        {
            ValidateSemester(dto.Semester);

            var journals =
                await LoadSemesterJournalsAsync(
                    dto.GroupId,
                    dto.Year,
                    dto.Semester);

            if (!journals.Any())
                return ServiceResult<JournalExportDto>.Fail(
                    "За вибраний семестр журнали не знайдено.");

            var workbook = new XLWorkbook();

            foreach (var journal in journals)
            {
                var students = await LoadStudentsAsync(journal);

                if (!students.Any())
                    continue;

                var grades = await LoadGradesAsync(journal.Id);

                var columns = BuildColumns(grades);

                var cellMap = BuildCellMap(grades);

                await CreateWorksheet(
                    workbook,
                    journal,
                    students,
                    columns,
                    cellMap);
            }

            if (!workbook.Worksheets.Any())
                return ServiceResult<JournalExportDto>.Fail(
                    "Не знайдено журналів з даними.");

            return ServiceResult<JournalExportDto>.Ok(
                BuildSemesterResult(
                    workbook,
                    dto.GroupId,
                    dto.Semester));
        }
        private async Task<List<JournalEntry>> LoadSemesterJournalsAsync(Guid groupId,int year,int semester)
        {
            var period = SemesterHelper.GetPeriod(year, semester);

            var journals = (await _journalRepository.GetByGroupIdAsync(groupId))
                .Where(j =>
                    j.Date >= period.Start &&
                    j.Date <= period.End)
                .OrderBy(j => j.Subject)
                .ThenBy(j => j.Date)
                .ToList();

            return journals;
        }
        private async Task<Dictionary<Guid, Group>> LoadGroupsAsync(List<JournalEntry> journals)
        {
            var ids = journals
                .Select(x => x.GroupId)
                .Distinct()
                .ToList();

            var groups = await _groupRepository.GetByIdsAsync(ids);

            return groups.ToDictionary(x => x.Id);
        }
        private List<JournalExportItemDto> BuildItems(
                                            List<JournalEntry> journals,
                                            Dictionary<Guid, Group> groups,
                                            Dictionary<Guid, User> teachers)
        {
            return journals
                .Select(j => new JournalExportItemDto
                {
                    JournalId = j.Id,
                    JournalName = j.Name,
                    GroupId = j.GroupId,
                    Subject = j.Subject,
                    GroupName = groups.TryGetValue(j.GroupId, out var group)
                            ? group.Name
                            : "Невідома група",
                    Teachers = j.TeacherId
                        .Where(id => teachers.ContainsKey(id))
                        .Select(id => teachers[id].FullName)
                        .ToList(),
                    Date = j.Date
                })
                .ToList();
        }
        private static int GetSemester(DateTime date)
        {
            return date.Month switch
            {
                >= 9 and <= 12 => 1,
                >= 1 and <= 6 => 2,
                _ => 0
            };
        }

        private async Task CreateWorksheet(
                    XLWorkbook workbook,
                    JournalEntry journal,
                    List<User> students,
                    List<JournalColumnDto> columns,
                    Dictionary<(Guid StudentId, DateTime Date, string Topic), Grade> cellMap)
        {
            var worksheet = workbook.Worksheets.Add(
                SanitizeWorksheetName(journal.Name));
            var teachers = await _userRepository.GetByIdsAsync(journal.TeacherId);

            var teacherNames = string.Join(", ",
                teachers.Select(x => x.FullName));
            WriteTitle(worksheet,journal,columns.Count,teacherNames);
            WriteHeader(worksheet, columns);

            var lastRow = WriteStudents(
                worksheet,
                students,
                columns,
                cellMap);

            WriteLegend(worksheet, lastRow);

            FormatWorksheet(
                worksheet,
                lastRow,
                columns.Count);
        }
        private JournalExportDto BuildSemesterResult(
                                XLWorkbook workbook,
                                Guid groupId,
                                int semester)
        {
            using var stream = new MemoryStream();

            workbook.SaveAs(stream);

            return new JournalExportDto
            {
                FileBytes = stream.ToArray(),
                FileName = $"Group_{groupId}_Semester_{semester}.xlsx"
            };
        }
        private static string SanitizeWorksheetName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Журнал";

            var invalid = new[] { "\\", "/", "*", "[", "]", ":", "?" };

            foreach (var ch in invalid)
                name = name.Replace(ch, "_");

            if (name.Length > 31)
                name = name[..31];

            return name;
        }
        private static void ValidateSemester(int semester)
        {
            if (semester != 1 && semester != 2)
                throw new ArgumentException("Невірний номер семестру.");
        }
        private async Task<Group?> LoadGroupAsync(Guid groupId)
        {
            return await _groupRepository.GetByIdAsync(groupId);
        }
        private async Task<Dictionary<Guid, User>> LoadTeachersAsync(List<JournalEntry> journals)
        {
            var teacherIds = journals
                .SelectMany(x => x.TeacherId)
                .Distinct()
                .ToList();

            var teachers = await _userRepository.GetByIdsAsync(teacherIds);

            return teachers.ToDictionary(x => x.Id);
        }
        private async Task<JournalEntry?> LoadJournalAsync(Guid journalId)
        {
            return await _journalRepository.GetByIdAsync(journalId);
        }
        private async Task<List<User>> LoadStudentsAsync(JournalEntry journal)
        {
            var group = await _groupRepository.GetByIdAsync(journal.GroupId);

            if (group?.StudentIds == null || group.StudentIds.Count == 0)
                return new();

            var students = await _userRepository.GetUsersByIdsAsync(group.StudentIds);

            return students
                .OrderBy(s => s.FullName)
                .ToList();
        }
        private async Task<List<Grade>> LoadGradesAsync(Guid journalId)
        {
            var grades = await _gradeRepository.GetByJournalEntryIdAsync(journalId);

            return grades
                .Where(g => g.Value.HasValue || g.IsPresent.HasValue)
                .ToList();
        }
        private List<JournalColumnDto> BuildColumns(List<Grade> grades)
        {
            return grades
                .GroupBy(g => new
                {
                    Date = g.Created.Date,
                    Topic = MakeTopicKey(g.Comment)
                })
                .Select(g => new JournalColumnDto
                {
                    Date = g.Key.Date,
                    Topic = g.Select(x => x.Comment)
                             .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "",

                    FirstCreated = g.Min(x => x.Created)
                })
                .OrderBy(x => x.Date)
                .ThenBy(x => x.FirstCreated)
                .ThenBy(x => x.Topic)
                .ToList();
        }
        private Dictionary<(Guid StudentId, DateTime Date, string Topic), Grade> BuildCellMap(List<Grade> grades)
        {
            return grades
                .GroupBy(g => new
                {
                    g.StudentId,
                    Date = g.Created.Date,
                    Topic = MakeTopicKey(g.Comment)
                })
                .ToDictionary(
                    g => (
                        g.Key.StudentId,
                        g.Key.Date,
                        g.Key.Topic),

                    g => g.OrderBy(x => x.Created)
                          .Last());
        }
        private async Task<XLWorkbook> CreateWorkbook(
    JournalEntry journal,
    List<User> students,
    List<JournalColumnDto> columns,
    Dictionary<(Guid StudentId, DateTime Date, string Topic), Grade> cellMap)
        {
            var workbook = new XLWorkbook();

            var worksheet = workbook.Worksheets.Add("Журнал");

            var teachers = await _userRepository.GetByIdsAsync(journal.TeacherId);

            var teacherNames = string.Join(", ",
                teachers.Select(x => x.FullName));

            WriteTitle(
                worksheet,
                journal,
                columns.Count,
                teacherNames);

            WriteHeader(worksheet, columns);

            var lastDataRow = WriteStudents(
                worksheet,
                students,
                columns,
                cellMap);

            WriteLegend(worksheet, lastDataRow);

            FormatWorksheet(
                worksheet,
                lastDataRow,
                columns.Count);

            return workbook;
        }
        private void WriteTitle(
    IXLWorksheet worksheet,
    JournalEntry journal,
    int columnsCount,
    string teacherNames)
        {
            worksheet.Cell(1, 1).Value = SanitizeTitle(journal.Name);

            worksheet.Range(1, 1, 1, columnsCount + 2)
                .Merge()
                .Style.Font.SetBold()
                .Font.SetFontSize(14);

            worksheet.Cell(2, 1).Value = $"Викладач: {teacherNames}";

            worksheet.Range(2, 1, 2, columnsCount + 2)
                .Merge()
                .Style.Font.SetItalic();

            worksheet.Row(1).Height = 24;
            worksheet.Row(2).Height = 20;

            worksheet.Range(1, 1, 2, columnsCount + 2)
                .Style.Alignment.Horizontal =
                    XLAlignmentHorizontalValues.Center;
        }
        private void WriteHeader(
                    IXLWorksheet worksheet,
                    List<JournalColumnDto> columns)
        {
            const int headerRow = 3;

            worksheet.Cell(headerRow, 1).Value = "№";
            worksheet.Cell(headerRow, 2).Value = "ПІБ студента";

            for (int i = 0; i < columns.Count; i++)
            {
                var column = columns[i];

                worksheet.Cell(headerRow, i + 3).Value =
                    $"{(string.IsNullOrWhiteSpace(column.Topic) ? "—" : column.Topic)}\n{column.Date:dd.MM}";

                worksheet.Cell(headerRow, i + 3)
                    .Style.Alignment.WrapText = true;
            }

            worksheet.Range(headerRow, 1, headerRow, columns.Count + 2)
                .Style.Font.SetBold()
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        }
        private int WriteStudents(
                    IXLWorksheet worksheet,
                    List<User> students,
                    List<JournalColumnDto> columns,
                    Dictionary<(Guid StudentId, DateTime Date, string Topic), Grade> cellMap)
        {
            int row = 4;
            int index = 1;

            foreach (var student in students)
            {
                worksheet.Cell(row, 1).Value = index++;
                worksheet.Cell(row, 2).Value = student.FullName;

                WriteStudentGrades(
                    worksheet,
                    row,
                    student,
                    columns,
                    cellMap);

                row++;
            }

            return row;
        }
        private void WriteStudentGrades(
                    IXLWorksheet worksheet,
                    int row,
                    User student,
                    List<JournalColumnDto> columns,
                    Dictionary<(Guid StudentId, DateTime Date, string Topic), Grade> cellMap)
        {
            for (int i = 0; i < columns.Count; i++)
            {
                var column = columns[i];

                var key = (
                    student.Id,
                    column.Date,
                    MakeTopicKey(column.Topic));

                if (cellMap.TryGetValue(key, out var grade))
                {
                    worksheet.Cell(row, i + 3).Value = BuildGradeText(grade);
                }
                else
                {
                    worksheet.Cell(row, i + 3).Value = "–";
                    worksheet.Cell(row, i + 3).Style.Font.FontColor = XLColor.Gray;
                }

                worksheet.Cell(row, i + 3).Style.Alignment.Horizontal =
                    XLAlignmentHorizontalValues.Center;
            }
        }
        private static string BuildGradeText(Grade grade)
        {
            var gradeText = grade.Value?.ToString() ?? "";

            var presenceText = grade.IsPresent switch
            {
                true => "П",
                false => "Н",
                null => ""
            };

            if (!string.IsNullOrEmpty(gradeText) &&
                !string.IsNullOrEmpty(presenceText))
            {
                return $"{gradeText} ({presenceText})";
            }

            if (!string.IsNullOrEmpty(gradeText))
                return gradeText;

            if (!string.IsNullOrEmpty(presenceText))
                return presenceText;

            return "–";
        }
        private void WriteLegend(
                    IXLWorksheet worksheet,
                    int lastRow)
        {
            worksheet.Cell(lastRow + 1, 1).Value =
                "П — присутній, Н — відсутній";

            worksheet.Range(lastRow + 1, 1, lastRow + 1, 3)
                .Merge();

            worksheet.Row(lastRow + 1).Style.Font.Italic = true;
        }
        private void FormatWorksheet(
                    IXLWorksheet worksheet,
                    int lastRow,
                    int columnsCount)
        {
            worksheet.Columns().AdjustToContents();

            worksheet.Column(2).Width =
                Math.Max(worksheet.Column(2).Width, 28);

            worksheet.Range(
                    1,
                    1,
                    lastRow - 1,
                    columnsCount + 2)
                .Style.Border.OutsideBorder =
                    XLBorderStyleValues.Thin;

            worksheet.Range(
                    1,
                    1,
                    lastRow - 1,
                    columnsCount + 2)
                .Style.Border.InsideBorder =
                    XLBorderStyleValues.Thin;
        }
        private JournalExportDto BuildResult(
                                XLWorkbook workbook,
                                string journalName)
        {
            using var stream = new MemoryStream();

            workbook.SaveAs(stream);

            return new JournalExportDto
            {
                FileBytes = stream.ToArray(),
                FileName = $"{SanitizeFileName(journalName)}.xlsx"
            };
        }
        private static string MakeTopicKey(string? topic)
        {
            return string.IsNullOrWhiteSpace(topic)
                ? "no-topic"
                : new string(
                    topic.Trim()
                         .ToLowerInvariant()
                         .Where(ch => char.IsLetterOrDigit(ch)
                                   || ch == '-'
                                   || ch == '_')
                         .ToArray());
        }
        private static string SanitizeFileName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "journal";

            var cleaned = Regex.Replace(
                input.Trim(),
                @"[^0-9A-Za-zА-Яа-яІіЇїЄєҐґ _\-()\.]",
                "_");

            cleaned = Regex.Replace(cleaned, @"\s+", " ");

            return cleaned;
        }
        private static string SanitizeTitle(string? name)
        {
            return string.IsNullOrWhiteSpace(name)
                ? "Журнал"
                : name.Trim();
        }
    }
}
