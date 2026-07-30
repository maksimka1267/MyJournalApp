using ClosedXML.Excel;
using MyJournalApp.Data.Dtos.Lesson;
using MyJournalApp.Helpers;
using MyJournalApp.Interface;
using MyJournalApp.Service.Interface;
using System.IO.Compression;

namespace MyJournalApp.Service
{
    public class LessonExportService : ILessonExportService
    {
        private readonly ILessonRepository _lessonRepository;
        private readonly IUserRepository _userRepository;
        private readonly IGroupRepository _groupRepository;

        public LessonExportService(
            ILessonRepository lessonRepository,
            IUserRepository userRepository,
            IGroupRepository groupRepository)
        {
            _lessonRepository = lessonRepository;
            _userRepository = userRepository;
            _groupRepository = groupRepository;
        }

        public async Task<LessonExportDto> ExportAsync(ExportDto dto)
        {
            if (!dto.TeacherId.HasValue || !dto.StartDate.HasValue || !dto.EndDate.HasValue)
            {
                throw new ArgumentException("Необходимо указать преподавателя и полный период (начальная и конечная даты) для формирования отчета.");
            }

            var filteredLessons = await _lessonRepository.GetByTeacherAsync(
                dto.TeacherId.Value,
                dto.StartDate.Value,
                dto.EndDate.Value,
                dto.GroupId,
                dto.SubjectName
            );

            if (filteredLessons.Count == 0)
            {
                throw new InvalidOperationException("Немає уроків, що відповідають заданим критеріям.");
            }

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Звіт по годинах");

                var firstLesson = filteredLessons.First();
                string groupNameForHeader = "Всі групи";
                Guid? effectiveGroupId = dto.GroupId;

                if (!effectiveGroupId.HasValue)
                {
                    var distinctGroups = filteredLessons.Select(l => l.GroupId).Distinct().ToList();
                    if (distinctGroups.Count == 1) effectiveGroupId = distinctGroups[0];
                }
                if (effectiveGroupId.HasValue)
                {
                    var group = await _groupRepository.GetByIdAsync(effectiveGroupId.Value);
                    groupNameForHeader = group?.Name ?? "Невідома група";
                }

                var user = await _userRepository.GetByIdAsync(firstLesson.TeacherId);

                worksheet.Cell("D2").Value = "Група:";
                worksheet.Cell("E2").Value = groupNameForHeader;
                worksheet.Cell("D4").Value = "Дисципліна:";
                worksheet.Cell("E4").Value = !string.IsNullOrEmpty(dto.SubjectName) ? dto.SubjectName : "Всі дисципліни";
                worksheet.Cell("D6").Value = "П.І.Б. викладача:";
                worksheet.Cell("E6").Value = user?.FullName ?? "Невідомий";

                // Заголовки таблиці
                var headerRow = 9;
                worksheet.Cell(headerRow, 1).Value = "Дата занять";
                worksheet.Cell(headerRow, 2).Value = "№ з/п";
                worksheet.Cell(headerRow, 3).Value = "Кількість годин";
                worksheet.Cell(headerRow, 4).Value = "Тема заняття";
                worksheet.Range(headerRow, 1, headerRow, 4).Style.Font.SetBold();
                worksheet.Range(headerRow, 1, headerRow, 4).Style.Fill.SetBackgroundColor(XLColor.LightGray);

                // Дані
                int currentRow = headerRow + 1;
                int lessonNumber = 1;
                foreach (var lesson in filteredLessons)
                {
                    worksheet.Cell(currentRow, 1).Value = lesson.StartTime.ToString("yyyy-MM-dd");
                    worksheet.Cell(currentRow, 2).Value = lessonNumber++;
                    worksheet.Cell(currentRow, 3).Value = lesson.Clocks.HasValue ? lesson.Clocks.Value.ToString() : "N/A";
                    worksheet.Cell(currentRow, 4).Value = lesson.Topic;
                    currentRow++;
                }

                worksheet.Column(1).AdjustToContents();
                worksheet.Column(2).AdjustToContents();
                worksheet.Column(3).AdjustToContents();
                worksheet.Column(4).Width = 50;

                using (var outStream = new MemoryStream())
                {
                    workbook.SaveAs(outStream);
                    return new LessonExportDto
                    {
                        Content = outStream.ToArray(),
                        ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        FileName = $"Export_Time_{user?.FullName}_{DateTime.Now:yyyyMMdd}.xlsx"
                    };
                }
            }
        }
        public async Task<LessonExportDto> ExportSemesterAsync(
    ExportSemesterLessonsDto dto)
        {
            ValidateSemester(dto.Semester);

            var lessons = await LoadLessonsAsync(
                dto.Year,
                dto.Semester);

            if (!lessons.Any())
                throw new InvalidOperationException("Уроків не знайдено.");

            var teachers = await LoadTeachersAsync(lessons);
            var groups = await LoadGroupsAsync(lessons);

            using var zipStream = new MemoryStream();

            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
            {
                var teacherGroups = lessons
                    .GroupBy(x => x.TeacherId);

                foreach (var teacherGroup in teacherGroups)
                {
                    var workbook = new XLWorkbook();

                    var groupedLessons = teacherGroup
                        .GroupBy(x => (x.GroupId, x.Name, x.TeacherId));

                    foreach (var lessonGroup in groupedLessons)
                    {
                        CreateWorksheet(
                            workbook,
                            lessonGroup.ToList(),
                            teachers,
                            groups);
                    }

                    var teacherName = teachers.TryGetValue(
                        teacherGroup.Key,
                        out var teacher)
                        ? teacher.FullName
                        : "Unknown";

                    var entry = archive.CreateEntry(
                        $"{SanitizeFileName(teacherName)}.xlsx");

                    using var entryStream = entry.Open();
                    using var workbookStream = new MemoryStream();

                    workbook.SaveAs(workbookStream);
                    workbookStream.Position = 0;
                    workbookStream.CopyTo(entryStream);
                }
            }

            return new LessonExportDto
            {
                Content = zipStream.ToArray(),
                ContentType = "application/zip",
                FileName = $"Hours_Semester_{dto.Semester}_{dto.Year}.zip"
            };
        }
        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Unknown";

            var invalid = Path.GetInvalidFileNameChars();

            foreach (var c in invalid)
                name = name.Replace(c, '_');

            return name;
        }
        private async Task<Dictionary<Guid, Group>> LoadGroupsAsync(
    List<Lesson> lessons)
        {
            var ids = lessons
                .Select(x => x.GroupId)
                .Distinct()
                .ToList();

            var groups = await _groupRepository.GetByIdsAsync(ids);

            return groups.ToDictionary(x => x.Id);
        }
        private static void ValidateSemester(int semester)
        {
            if (semester != 1 && semester != 2)
                throw new ArgumentException("Невірний семестр.");
        }
        private async Task<List<Lesson>> LoadLessonsAsync(
    int year,
    int semester)
        {
            var period = SemesterHelper.GetPeriod(year, semester);

            return await _lessonRepository.GetByPeriodAsync(
                period.Start,
                period.End);
        }
        private void CreateWorksheet(
    XLWorkbook workbook,
    List<Lesson> lessons,
    Dictionary<Guid, User> teachers,
    Dictionary<Guid, Group> groups)
        {
            var first = lessons.First();

            var teacherName = teachers.TryGetValue(first.TeacherId, out var teacher)
                ? teacher.FullName
                : "Невідомий";

            var groupName = groups.TryGetValue(first.GroupId, out var group)
                ? group.Name
                : "Невідома група";

            var worksheet = workbook.Worksheets.Add(
                GetUniqueWorksheetName(
                    workbook,
                    $"{groupName}_{first.Name}_{teacherName}"));

            worksheet.Cell(1, 1).Value = "Предмет";
            worksheet.Cell(1, 2).Value = first.Name;

            worksheet.Cell(2, 1).Value = "Викладач";
            worksheet.Cell(2, 2).Value = teacherName;

            worksheet.Cell(4, 1).Value = "Дата";
            worksheet.Cell(4, 2).Value = "Тема";
            worksheet.Cell(4, 3).Value = "Години";

            worksheet.Range(4, 1, 4, 3)
                .Style.Font.Bold = true;

            int row = 5;

            foreach (var lesson in lessons.OrderBy(x => x.StartTime))
            {
                worksheet.Cell(row, 1).Value =
                    lesson.StartTime.ToString("dd.MM.yyyy");

                worksheet.Cell(row, 2).Value =
                    lesson.Topic;

                worksheet.Cell(row, 3).Value =
                    lesson.Clocks ?? 0;

                row++;
            }

            worksheet.Cell(row + 1, 2).Value = "Разом";

            int firstDataRow = 5;
            int lastDataRow = row - 1;
            worksheet.Range(1, 1, 2, 2).Style.Font.Bold = true;
            worksheet.Cell(row + 1, 3).FormulaA1 =
                $"SUM(C{firstDataRow}:C{lastDataRow})";

            worksheet.Columns().AdjustToContents();
        }
        private static string GetUniqueWorksheetName(XLWorkbook workbook,string desiredName)
        {
            desiredName = SanitizeWorksheetName(desiredName);

            var name = desiredName;
            int index = 1;

            while (workbook.Worksheets.Any(w => w.Name == name))
            {
                var suffix = $"_{index++}";

                var maxLength = 31 - suffix.Length;

                name = desiredName.Length > maxLength
                    ? desiredName[..maxLength] + suffix
                    : desiredName + suffix;
            }

            return name;
        }
        private static string SanitizeWorksheetName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Аркуш";

            var invalid = new[] { "\\", "/", "*", "[", "]", ":", "?" };

            foreach (var c in invalid)
                name = name.Replace(c, "_");

            return name.Length > 31
                ? name[..31]
                : name;
        }
        private async Task<Dictionary<Guid, User>> LoadTeachersAsync(
    List<Lesson> lessons)
        {
            var ids = lessons
                .Select(l => l.TeacherId)
                .Distinct()
                .ToList();

            var teachers = await _userRepository.GetByIdsAsync(ids);

            return teachers.ToDictionary(x => x.Id);
        }
    }
}
