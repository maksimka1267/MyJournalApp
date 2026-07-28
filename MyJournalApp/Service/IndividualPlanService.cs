using ClosedXML.Excel;
using Microsoft.AspNetCore.Hosting;
using MyJournalApp.Dtos.IndividualPlan;
using MyJournalApp.Interface;
using MyJournalApp.Result;
using MyJournalApp.Service.Interface;
using System.Text.RegularExpressions;

public class IndividualPlanService : IIndividualPlanService
{
    private readonly IStudentRepository _studentRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly IJournalEntryRepository _journalRepository;
    private readonly IGradeRepository _gradeRepository;
    private readonly IUserRepository _userRepository;
    private readonly IWebHostEnvironment _environment;

    private const string GroupFilesFolder = "group-files";

    public IndividualPlanService(
        IStudentRepository studentRepository,
        IGroupRepository groupRepository,
        IJournalEntryRepository journalRepository,
        IGradeRepository gradeRepository,
        IUserRepository userRepository,
        IWebHostEnvironment environment)
    {
        _studentRepository = studentRepository;
        _groupRepository = groupRepository;
        _journalRepository = journalRepository;
        _gradeRepository = gradeRepository;
        _userRepository = userRepository;
        _environment = environment;
    }
    public async Task<ServiceResult<IndividualPlanFileDto>> DownloadForMeAsync(
    Guid currentUserId,
    int? semester)
    {
        var user = await _userRepository.GetByIdAsync(currentUserId);

        if (user == null ||
            !string.Equals(user.Role, "Student", StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<IndividualPlanFileDto>.Fail("Access denied");
        }

        return await BuildIndividualPlanAsync(
            currentUserId,
            semester);
    }
    public async Task<ServiceResult<IndividualPlanFileDto>> DownloadForStudentAsync(
    Guid currentUserId,
    DownloadIndividualPlanRequestDto dto)
    {
        var currentUser = await _userRepository.GetByIdAsync(currentUserId);

        if (currentUser == null)
        {
            return ServiceResult<IndividualPlanFileDto>.Fail("Access denied");
        }

        var isAdminOrTeacher =
            string.Equals(currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(currentUser.Role, "Teacher", StringComparison.OrdinalIgnoreCase);

        var isSelf = currentUser.Id == dto.StudentId;

        if (!isAdminOrTeacher && !isSelf)
        {
            return ServiceResult<IndividualPlanFileDto>.Fail("Access denied");
        }

        return await BuildIndividualPlanAsync(
            dto.StudentId,
            dto.Semester);
    }
    private async Task<ServiceResult<IndividualPlanFileDto>> BuildIndividualPlanAsync(
    Guid studentId,
    int? semester)
    {
        var student = await _studentRepository.GetByIdAsync(studentId);

        if (student == null || student.GroupId == Guid.Empty)
        {
            return ServiceResult<IndividualPlanFileDto>.Fail(
                "Студента або його групу не знайдено.");
        }

        var group = await _groupRepository.GetByIdAsync(student.GroupId);

        if (group == null)
        {
            return ServiceResult<IndividualPlanFileDto>.Fail(
                "Групу не знайдено.");
        }

        var user = await _userRepository.GetByIdAsync(studentId);

        var studentName = user?.FullName ?? "Студент";

        DateTime semesterStart;
        DateTime semesterEnd;
        int semesterNumber;

        if (semester is 1 or 2)
        {
            GetSemesterRangeByChoice(
                DateTime.Today,
                semester.Value,
                out semesterStart,
                out semesterEnd,
                out semesterNumber);
        }
        else
        {
            GetSemesterRangeAuto(
                DateTime.Today,
                out semesterStart,
                out semesterEnd,
                out semesterNumber);
        }

        var templatePath = BuildTemplatePath(
            group.Name,
            semesterNumber);

        if (!File.Exists(templatePath))
        {
            return ServiceResult<IndividualPlanFileDto>.Fail(
                "Файл шаблону для цього семестру відсутній.");
        }

        var grades = await _gradeRepository.GetByStudentIdsAndDateRangeAsync(
            new[] { studentId },
            semesterStart,
            semesterEnd);

        var journals = await _journalRepository.GetByGroupIdAsync(group.Id);

        var subjectKeyByJournalId = journals.ToDictionary(
            j => j.Id,
            j => MakeSubjectKey(
                ExtractSubjectFromName(
                    j.Name,
                    j.Subject)));

        var gradeMap = BuildGradeMap(
            grades,
            subjectKeyByJournalId);

        return await GenerateExcelAsync(
            templatePath,
            studentName,
            group.Name,
            gradeMap);
    }
    private string BuildTemplatePath(
    string groupName,
    int semester)
    {
        var baseDir =
            _environment.WebRootPath ??
            _environment.ContentRootPath;

        return Path.Combine(
            baseDir,
            GroupFilesFolder,
            $"{SanitizeFileName(groupName)}_sem{semester}.xlsx");
    }
    private Dictionary<string, List<(DateTime dt, int? value, string? comment)>> BuildGradeMap(
    IEnumerable<Grade> grades,
    Dictionary<Guid, string> subjectKeyByJournalId)
    {
        var result =
            new Dictionary<string, List<(DateTime, int?, string?)>>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var grade in grades.OrderBy(g => g.Created))
        {
            if (!subjectKeyByJournalId.TryGetValue(
                grade.JournalEntryId,
                out var subjectKey))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(subjectKey))
                continue;

            if (!result.TryGetValue(subjectKey, out var list))
            {
                list = new List<(DateTime, int?, string?)>();

                result[subjectKey] = list;
            }

            list.Add((
                grade.Created,
                grade.Value,
                grade.Comment));
        }

        return result;
    }
    private async Task<ServiceResult<IndividualPlanFileDto>> GenerateExcelAsync(
    string templatePath,
    string studentName,
    string groupName,
    Dictionary<string, List<(DateTime dt, int? value, string? comment)>> gradeMap)
    {
        var physicalSubjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        MakeSubjectKey("Фізична культура"),
        MakeSubjectKey("Фізичне виховання")
    };

        using var workbook = new XLWorkbook(templatePath);
        var worksheet = workbook.Worksheet(1);

        SetValueByLabel(
            worksheet,
            "ЗДОБУВАЧ ОСВІТИ",
            studentName);

        SetValueByLabel(
            worksheet,
            "ГРУПА",
            groupName);

        var headersResult = FindHeaders(
            worksheet,
            new[]
            {
            "ПРЕДМЕТ",
            "Форма контролю",
            "Оцінка"
            });

        var headers = headersResult.Cols;
        var headerRow = headersResult.HeaderRow;

        if (!headers.TryGetValue("ПРЕДМЕТ", out var subjectColumn) ||
            !headers.TryGetValue("Форма контролю", out var formColumn) ||
            !headers.TryGetValue("Оцінка", out var gradeColumn))
        {
            return ServiceResult<IndividualPlanFileDto>.Fail(
                "Не знайдено потрібні заголовки у шаблоні.");
        }

        FillGrades(
            worksheet,
            headerRow,
            subjectColumn,
            formColumn,
            gradeColumn,
            gradeMap,
            physicalSubjects);

        using var stream = new MemoryStream();

        workbook.SaveAs(stream);

        return ServiceResult<IndividualPlanFileDto>.Ok(
            new IndividualPlanFileDto
            {
                Content = stream.ToArray(),
                FileName = $"Індивідуальний_план_{SanitizeFileName(studentName)}.xlsx"
            });
    }
    private void FillGrades(
    IXLWorksheet worksheet,
    int headerRow,
    int subjectColumn,
    int formColumn,
    int gradeColumn,
    Dictionary<string, List<(DateTime dt, int? value, string? comment)>> gradeMap,
    HashSet<string> physicalSubjects)
    {
        var row = headerRow + 1;

        while (true)
        {
            var subjectText = NormalizeText(
                worksheet.Cell(row, subjectColumn).GetString());

            if (string.IsNullOrWhiteSpace(subjectText))
                break;

            var subjectKey = MakeSubjectKey(subjectText);

            var formText = NormalizeText(
                worksheet.Cell(row, formColumn).GetString());

            var formKey = MakeFormKey(formText);

            var grade = ResolveGrade(
                subjectKey,
                formKey,
                gradeMap,
                physicalSubjects);

            worksheet.Cell(row, gradeColumn).Value = grade;

            row++;
        }
    }
    private string ResolveGrade(
    string subjectKey,
    string formKey,
    Dictionary<string, List<(DateTime dt, int? value, string? comment)>> gradeMap,
    HashSet<string> physicalSubjects)
    {
        if (!gradeMap.TryGetValue(subjectKey, out var grades) ||
            grades.Count == 0)
        {
            return "-";
        }

        if (physicalSubjects.Contains(subjectKey))
        {
            var hasCredit = grades.Any(g =>
                (MakeFormKey(g.comment) == formKey || g.comment == null) &&
                g.value == 30);

            return hasCredit
                ? "зараховано"
                : "-";
        }

        var lastGrade = grades
            .Where(g =>
                MakeFormKey(g.comment) == formKey &&
                g.value.HasValue)
            .OrderBy(g => g.dt)
            .LastOrDefault();

        return lastGrade.value?.ToString() ?? "-";
    }
    private static string ExtractSubjectFromName(string? name, string? fallbackSubject)
    {
        var baseName = NormalizeText(name);

        if (string.IsNullOrWhiteSpace(baseName))
            return NormalizeText(fallbackSubject) ?? "Предмет";

        var idx = baseName.IndexOf(" - ", StringComparison.Ordinal);

        var beforeDash = idx >= 0
            ? baseName[..idx]
            : baseName;

        return NormalizeText(beforeDash);
    }

    private static string NormalizeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        text = text.Replace('\u00A0', ' ').Trim();

        return Regex.Replace(text, @"\s+", " ");
    }

    private static string MakeSubjectKey(string? subject)
    {
        subject = NormalizeText(subject);

        if (string.IsNullOrWhiteSpace(subject))
            return "no-subject";

        subject = subject.ToLowerInvariant();

        subject = new string(subject
            .Where(c =>
                char.IsLetterOrDigit(c) ||
                c == ' ' ||
                c == '-' ||
                c == '_')
            .ToArray());

        subject = Regex.Replace(subject, @"\s+", " ").Trim();

        return string.IsNullOrWhiteSpace(subject)
            ? "no-subject"
            : subject;
    }

    private static string MakeFormKey(string? form)
    {
        form = NormalizeText(form);

        if (string.IsNullOrWhiteSpace(form))
            return "no-topic";

        var key = MakeTopicKey(form);

        if (key == MakeTopicKey("Захист ДП"))
            return MakeTopicKey("Захист КП/КР");

        return key;
    }

    private static string MakeTopicKey(string? topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
            return "no-topic";

        return new string(
            NormalizeText(topic)
                .ToLowerInvariant()
                .Where(c =>
                    char.IsLetterOrDigit(c) ||
                    c == '-' ||
                    c == '_')
                .ToArray());
    }
    private static void GetSemesterRangeAuto(
    DateTime today,
    out DateTime start,
    out DateTime end,
    out int semesterNumber)
    {
        var academicYear = GetAcademicYearStart(today);

        if (today.Month >= 9 || today.Month == 1)
        {
            semesterNumber = 1;

            start = new DateTime(academicYear, 9, 1);
            end = new DateTime(academicYear + 1, 1, 31);
        }
        else
        {
            semesterNumber = 2;

            start = new DateTime(academicYear + 1, 2, 1);
            end = new DateTime(academicYear + 1, 7, 31);
        }
    }

    private static void GetSemesterRangeByChoice(
        DateTime today,
        int semester,
        out DateTime start,
        out DateTime end,
        out int semesterNumber)
    {
        semester = semester == 2 ? 2 : 1;

        semesterNumber = semester;

        var academicYear = GetAcademicYearStart(today);

        if (semester == 1)
        {
            start = new DateTime(academicYear, 9, 1);
            end = new DateTime(academicYear + 1, 1, 31);
        }
        else
        {
            start = new DateTime(academicYear + 1, 2, 1);
            end = new DateTime(academicYear + 1, 7, 31);
        }
    }

    private static int GetAcademicYearStart(DateTime today)
    {
        return today.Month >= 9
            ? today.Year
            : today.Year - 1;
    }
    private sealed record HeaderFindResult(Dictionary<string, int> Cols, int HeaderRow);

    private static void SetValueByLabel(
        IXLWorksheet ws,
        string label,
        string value,
        int maxRowsToScan = 50,
        int maxColsToScan = 30)
    {
        for (int r = 1; r <= maxRowsToScan; r++)
        {
            for (int c = 1; c <= maxColsToScan; c++)
            {
                var txt = NormalizeText(ws.Cell(r, c).GetString());
                if (txt.Equals(label, StringComparison.CurrentCultureIgnoreCase))
                {
                    // спробувати знайти жовту справа
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

                    // інакше — найближча справа
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

        for (int r = 1; r <= 50; r++)
        {
            for (int c = 1; c <= 50; c++)
            {
                var txt = NormalizeText(ws.Cell(r, c).GetString());

                if (wanted.Contains(txt) && !found.ContainsKey(txt))
                {
                    found[txt] = c;
                    headerRow = Math.Max(headerRow, r);
                }

                if (found.Count == wanted.Count)
                    return new HeaderFindResult(found, headerRow);
            }
        }

        return new HeaderFindResult(found, headerRow);
    }
    private static string SanitizeFileName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "file";

        var cleaned = Regex.Replace(
            input.Trim(),
            @"[^0-9A-Za-zА-Яа-яІіЇїЄєҐґ _\-]",
            "_");

        cleaned = Regex.Replace(
            cleaned,
            @"\s+",
            " ");

        return cleaned;
    }
}