using MyJournalApp.Data.Dtos.StudentGrades;
using MyJournalApp.Interface;
using MyJournalApp.Service.Interface;

namespace MyJournalApp.Service
{
    public class StudentGradesReportService : IStudentGradesReportService
    {
        private readonly IGradeRepository _gradeRepository;
        private readonly IUserRepository _userRepository;
        private readonly IJournalEntryRepository _journalRepository;

        public StudentGradesReportService(
            IGradeRepository gradeRepository,
            IUserRepository userRepository,
            IJournalEntryRepository journalRepository)
        {
            _gradeRepository = gradeRepository;
            _userRepository = userRepository;
            _journalRepository = journalRepository;
        }

        public async Task<StudentGradesReportDto> BuildReportAsync(
            Guid studentId,
            DateTime start,
            DateTime end)
        {
            Validate(studentId, start, end);

            var student = await LoadStudentAsync(studentId);

            var dates = BuildDateRange(start, end);

            var grades = await LoadGradesAsync(studentId, start, end);

            var subjectDictionary = await BuildSubjectDictionaryAsync(grades);

            var rows = BuildRows(
                grades,
                dates,
                subjectDictionary);

            return new StudentGradesReportDto
            {
                StudentId = studentId,
                StudentName = student.FullName,
                StartDate = start.Date,
                EndDate = end.Date,
                Dates = dates,
                Rows = rows
            };
        }
        private static void Validate(Guid studentId, DateTime start, DateTime end)
        {
            if (studentId == Guid.Empty)
                throw new ArgumentException("Необхідно вказати студента.");

            if (start == default || end == default)
                throw new ArgumentException("Необхідно вказати період.");

            if (start > end)
                throw new ArgumentException("Дата початку не може бути більшою за дату завершення.");
        }
        private async Task<User> LoadStudentAsync(Guid studentId)
        {
            var student = await _userRepository.GetByIdAsync(studentId);

            if (student == null)
                throw new KeyNotFoundException("Студента не знайдено.");

            return student;
        }
        private static List<DateTime> BuildDateRange(DateTime start, DateTime end)
        {
            return Enumerable
                .Range(0, (end.Date - start.Date).Days + 1)
                .Select(i => start.Date.AddDays(i))
                .ToList();
        }
        private async Task<List<Grade>> LoadGradesAsync(
    Guid studentId,
    DateTime start,
    DateTime end)
        {
            return (await _gradeRepository.GetByStudentIdsAndDateRangeAsync(
                new[] { studentId },
                start,
                end))
                .ToList();
        }
        private async Task<Dictionary<Guid, string>> BuildSubjectDictionaryAsync(
    IEnumerable<Grade> grades)
        {
            var result = new Dictionary<Guid, string>();

            var journalIds = grades
                .Select(g => g.JournalEntryId)
                .Distinct();

            foreach (var journalId in journalIds)
            {
                var journal = await _journalRepository.GetByIdAsync(journalId);

                if (journal != null)
                {
                    result[journalId] =
                        string.IsNullOrWhiteSpace(journal.Name)
                            ? journal.Subject ?? "Предмет"
                            : journal.Name;
                }
            }

            return result;
        }
        private static List<SubjectRowDto> BuildRows(
    IEnumerable<Grade> grades,
    List<DateTime> dates,
    Dictionary<Guid, string> subjectDictionary)
        {
            return grades
                .GroupBy(g => g.JournalEntryId)
                .Select(group =>
                {
                    var subject = subjectDictionary.GetValueOrDefault(
                        group.Key,
                        "Предмет");

                    var cells = dates.ToDictionary(
                        d => d.ToString("yyyyMMdd"),
                        _ => new List<int>());

                    foreach (var day in group
                                 .GroupBy(x => x.Created.Date)
                                 .OrderBy(x => x.Key))
                    {
                        var values = day
                            .OrderBy(x => x.Created)
                            .Select(x => x.Value)
                            .Where(x => x.HasValue)
                            .Select(x => x!.Value)
                            .ToList();

                        if (values.Count > 0)
                        {
                            cells[day.Key.ToString("yyyyMMdd")] = values;
                        }
                    }

                    return new SubjectRowDto
                    {
                        SubjectName = subject,
                        Cells = cells
                    };
                })
                .OrderBy(x => x.SubjectName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
    }
}