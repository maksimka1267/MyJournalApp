using ClosedXML.Excel;
using MyJournalApp.Data.Dtos.StudentGrades;
using MyJournalApp.Service.Interface;

namespace MyJournalApp.Service
{
    public class StudentGradesExportService : IStudentGradesExportService
    {
        private readonly IStudentGradesReportService _reportService;

        public StudentGradesExportService(
            IStudentGradesReportService reportService)
        {
            _reportService = reportService;
        }
        public async Task<StudentGradesExportDto> ExportAsync(
                                                    Guid studentId,
                                                    DateTime start,
                                                    DateTime end)
        {
            var dto = await _reportService.BuildReportAsync(
                studentId,
                start,
                end);

            using var workbook = new XLWorkbook();

            var worksheet = workbook.Worksheets.Add("Рапортичка оцінок");

            var multiplicity = BuildMultiplicity(dto);

            var columnCount = 2 + multiplicity.Values.Sum();

            BuildHeader(
                worksheet,
                dto,
                multiplicity,
                columnCount);

            var lastRow = FillRows(
                worksheet,
                dto,
                multiplicity);

            ApplyStyle(
                worksheet,
                lastRow,
                columnCount);

            using var stream = new MemoryStream();

            workbook.SaveAs(stream);

            return new StudentGradesExportDto
            {
                Content = stream.ToArray(),
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                FileName = $"файл з оцінками студента {SanitizeFileName(dto.StudentName)}.xlsx"
            };
        }
        private static Dictionary<DateTime, int> BuildMultiplicity(StudentGradesReportDto dto)
        {
            var result = new Dictionary<DateTime, int>();

            foreach (var date in dto.Dates)
            {
                var key = date.ToString("yyyyMMdd");

                var maxCount = dto.Rows
                    .Select(r => r.Cells.TryGetValue(key, out var list) ? list.Count : 0)
                    .DefaultIfEmpty(0)
                    .Max();

                result[date] = Math.Max(1, maxCount);
            }

            return result;
        }
        private static void BuildHeader(
                            IXLWorksheet ws,
                            StudentGradesReportDto dto,
                            Dictionary<DateTime, int> multiplicityByDate,
                            int columnCount)
        {
            ws.Cell(1, 1).Value =
                $"Рапортичка оцінок студента {dto.StudentName}";

            ws.Range(1, 1, 1, columnCount)
                .Merge()
                .Style.Font.SetBold()
                .Font.SetFontSize(14);

            ws.Row(1).Height = 24;
            ws.Row(1).Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            ws.Cell(2, 1).Value = "№";
            ws.Cell(2, 2).Value = "Предмет";

            ws.Range(2, 1, 3, 1).Merge();
            ws.Range(2, 2, 3, 2).Merge();

            int currentColumn = 3;

            foreach (var date in dto.Dates)
            {
                var count = multiplicityByDate[date];

                if (count == 1)
                {
                    ws.Cell(2, currentColumn).Value =
                        date.ToString("dd.MM");

                    ws.Range(2, currentColumn, 3, currentColumn)
                        .Merge();

                    currentColumn++;
                }
                else
                {
                    ws.Range(
                            2,
                            currentColumn,
                            2,
                            currentColumn + count - 1)
                        .Merge()
                        .Value = date.ToString("dd.MM");

                    for (int i = 0; i < count; i++)
                    {
                        ws.Cell(3, currentColumn + i).Value = i + 1;
                    }

                    currentColumn += count;
                }
            }

            ws.Range(2, 1, 3, columnCount)
                .Style.Font.SetBold()
                .Alignment.SetHorizontal(
                    XLAlignmentHorizontalValues.Center)
                .Alignment.SetVertical(
                    XLAlignmentVerticalValues.Center);
        }
        private static int FillRows(
    IXLWorksheet ws,
    StudentGradesReportDto dto,
    Dictionary<DateTime, int> multiplicityByDate)
        {
            int row = 4;
            int number = 1;

            foreach (var subject in dto.Rows)
            {
                ws.Cell(row, 1).Value = number++;
                ws.Cell(row, 2).Value = subject.SubjectName;

                int currentColumn = 3;

                foreach (var date in dto.Dates)
                {
                    var key = date.ToString("yyyyMMdd");

                    var width = multiplicityByDate[date];

                    subject.Cells.TryGetValue(key, out var grades);

                    grades ??= new List<int>();

                    for (int i = 0; i < width; i++)
                    {
                        var cell = ws.Cell(row, currentColumn + i);

                        if (i < grades.Count)
                        {
                            cell.Value = grades[i];
                        }
                        else
                        {
                            cell.Value = "–";
                            cell.Style.Font.FontColor = XLColor.Gray;
                        }

                        cell.Style.Alignment.Horizontal =
                            XLAlignmentHorizontalValues.Center;
                    }

                    currentColumn += width;
                }

                row++;
            }

            return row;
        }
        private static void ApplyStyle(
                            IXLWorksheet ws,
                            int lastRow,
                            int columnCount)
        {
            ws.Columns().AdjustToContents();

            ws.Range(1, 1, lastRow - 1, columnCount)
                .Style.Border.OutsideBorder =
                XLBorderStyleValues.Thin;

            ws.Range(1, 1, lastRow - 1, columnCount)
                .Style.Border.InsideBorder =
                XLBorderStyleValues.Thin;
        }
        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();

            return string.Join(
                    "_",
                    name.Split(
                        invalid,
                        StringSplitOptions.RemoveEmptyEntries))
                .TrimEnd('.');
        }
    }
}