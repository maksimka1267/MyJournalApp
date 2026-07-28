using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyJournalApp.Data.Models;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace MyJournalApp.Pages;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _contextAccessor;

    public IndexModel(IHttpClientFactory httpClientFactory, IHttpContextAccessor contextAccessor)
    {
        _httpClientFactory = httpClientFactory;
        _contextAccessor = contextAccessor;
    }

    public readonly Dictionary<Guid, string> _teacherNameCache = new();
    public string Role { get; set; } = "";
    [BindProperty] public string? BulkChangesJson { get; set; }  // заполняет JS
    [BindProperty] public DateTime StartDate { get; set; }
    [BindProperty] public DateTime EndDate { get; set; }
    [BindProperty]
    public ExportSemesterLessonsFormDto ExportSemester { get; set; } = new();

    // ---- DTOs для клиентской сборки изменений ----
    public class BulkChangeDto
    {
        public Guid Id { get; set; }             // lessonId (базового дня)
        public string? Topic { get; set; }       // null = поле не трогали; "" = стереть
        public string? Homework { get; set; }    // null = поле не трогали; "" = стереть
        public int? Clocks { get; set; }         // null = поле не трогали
        public string? Name { get; set; }        // опционально: inline-редактирование предмета
        public Guid? TeacherId { get; set; }     // опционально: inline-редактирование викладача
        public DateTime StartDate { get; set; }  // yyyy-MM-dd из data-lesson-date (дата базового дня)
        public bool? Delete { get; set; }        // true => удалить слот по диапазону
    }
    public class ExportSemesterLessonsFormDto
    {
        public int Year { get; set; }

        public int Semester { get; set; }
    }
    // ---- DTO, который отправляем в API /api/Lesson/bulk-apply ----
    public class BulkApplyLessonDto
    {
        public Guid Id { get; set; }
        public Guid GroupId { get; set; }
        public DateTime StartTime { get; set; }

        public string? Name { get; set; }
        public string? Topic { get; set; }
        public string? Homework { get; set; }
        public int? Clocks { get; set; }
        public Guid TeacherId { get; set; }
        public Guid? SecondTeacherId { get; set; }

        public bool Delete { get; set; }
    }

    public List<string> GroupSubjects { get; set; } = new();
    public List<Group> AllGroups { get; set; } = new();
    public List<Lesson> DayLessons { get; set; } = new();
    public List<User> Teachers { get; set; } = new();
    public Teacher? CurrentTeacher { get; set; }
    public List<DateOnly> WeekDays { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public Guid SelectedGroupId { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly SelectedDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [BindProperty(SupportsGet = true)]
    public bool BulkMode { get; set; }  // чтобы включать/выключать через query

    [TempData]
    public DateTime? LastEditedDateIso { get; set; } // дата последнего измененного дня

    [BindProperty] public DateTime BulkStartDate { get; set; } // из hidden (заполняет JS)
    [BindProperty] public DateTime BulkEndDate { get; set; }   // из input[type=date] (заполняет JS)

    [BindProperty] public LessonFormDto InputLesson { get; set; } = new();
    [BindProperty] public ExportFormDto ExportFilters { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var token = Request.Cookies["cookies"];
        if (string.IsNullOrEmpty(token)) return RedirectToPage("/Account/Login");

        var user = _contextAccessor.HttpContext?.User;
        var userId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Role = user?.FindFirst(ClaimTypes.Role)?.Value ?? "";

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (Role == "Admin")
        {
            var groupResponse = await client.GetAsync(ApiUrl("/api/Group/all"));
            if (groupResponse.IsSuccessStatusCode)
            {
                var json = await groupResponse.Content.ReadAsStringAsync();
                AllGroups = JsonSerializer.Deserialize<List<Group>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            }

            var teacherResponse = await client.GetAsync(ApiUrl("/api/User/teachers"));
            if (teacherResponse.IsSuccessStatusCode)
            {
                var json = await teacherResponse.Content.ReadAsStringAsync();
                Teachers = JsonSerializer.Deserialize<List<User>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            }

            await LoadSubjectsForGroupAsync(client, SelectedGroupId);
        }

        if (Role == "Student")
        {
            if (Guid.TryParse(userId, out var studentId))
            {
                var studentResponse = await client.GetAsync(ApiUrl($"/api/User/student/{studentId}"));
                if (studentResponse.IsSuccessStatusCode)
                {
                    var json = await studentResponse.Content.ReadAsStringAsync();
                    var student = JsonSerializer.Deserialize<Student>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    SelectedGroupId = student?.GroupId ?? Guid.Empty;
                }
            }
        }

        if (SelectedGroupId == Guid.Empty && AllGroups.Any())
        {
            AllGroups = AllGroups.OrderBy(g => g.Name).ToList();
            SelectedGroupId = AllGroups.First().Id;
        }

        if (SelectedGroupId != Guid.Empty)
        {
            var url = ApiUrl($"/api/Lesson/group/{SelectedGroupId}/date/{SelectedDate.ToDateTime(TimeOnly.MinValue):yyyy-MM-dd}");
            var response = await client.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                DayLessons = JsonSerializer.Deserialize<List<Lesson>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            }
        }
        else if (Role == "Teacher")
        {
            var groupResponse = await client.GetAsync(ApiUrl("/api/Group/all"));
            if (groupResponse.IsSuccessStatusCode)
            {
                var json = await groupResponse.Content.ReadAsStringAsync();
                AllGroups = JsonSerializer.Deserialize<List<Group>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            }

            if (Guid.TryParse(userId, out var teacherId))
            {
                var response = await client.GetAsync(ApiUrl("/api/Lesson"));
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var allLessons = JsonSerializer.Deserialize<List<Lesson>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                    DayLessons = allLessons
                        .Where(l =>
                            DateOnly.FromDateTime(l.StartTime.Date) == SelectedDate &&
                            (l.TeacherId == teacherId || (l.SecondTeacherId.HasValue && l.SecondTeacherId.Value == teacherId))
                        )
                        .ToList();
                }

                var teacherResponse = await client.GetAsync(ApiUrl($"/api/User/teacher-model/{teacherId}"));
                if (teacherResponse.IsSuccessStatusCode)
                {
                    var json = await teacherResponse.Content.ReadAsStringAsync();
                    CurrentTeacher = JsonSerializer.Deserialize<Teacher>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
            }
        }

        WeekDays = GetWeekDays(SelectedDate);
        await PreloadTeacherNames();
        return Page();
    }

    private async Task LoadSubjectsForGroupAsync(HttpClient client, Guid groupId)
    {
        if (groupId == Guid.Empty) return;

        var url = ApiUrl($"/api/Lesson/group/{groupId}/subjects");
        var response = await client.GetAsync(url);
        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();
            GroupSubjects = JsonSerializer.Deserialize<List<string>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }
    }

    public async Task<IActionResult> OnPostAsync(string? handler)
    {
        var token = Request.Cookies["cookies"];
        if (string.IsNullOrEmpty(token)) return RedirectToPage("/Account/Login");

        var user = _contextAccessor.HttpContext?.User;
        Role = user?.FindFirst(ClaimTypes.Role)?.Value ?? "";
        var userId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(Role)) return Unauthorized();

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        switch (handler)
        {
            case "ImportLessons":
                if (Role != "Admin") return Forbid();

                if (Request.Form.Files.Count > 0 && Request.Form.Files["File"] is IFormFile file)
                {
                    var groupIdRaw = Request.Form["GroupId"];
                    var isNumeratorRaw = Request.Form["IsNumerator"];
                    var startDateRaw = Request.Form["StartDate"];
                    var endDateRaw = Request.Form["EndDate"];

                    if (Guid.TryParse(groupIdRaw, out var groupId)
                        && bool.TryParse(isNumeratorRaw, out var isNumerator)
                        && DateTime.TryParse(startDateRaw, out var startDate)
                        && DateTime.TryParse(endDateRaw, out var endDate))
                    {
                        startDate = startDate.Date;
                        endDate = endDate.Date;

                        if (endDate < startDate)
                        {
                            TempData["ErrorMessage"] = "Кінцева дата не може бути раніше за початкову.";
                            return RedirectToPage(new { SelectedGroupId = groupId, SelectedDate, BulkMode });
                        }

                        await ImportLessonsAsync(client, file, groupId, isNumerator, startDate, endDate);

                        // возвращаемся на выбранную дату (можно оставить SelectedDate как было)
                        return RedirectToPage(new { SelectedGroupId = groupId, SelectedDate });
                    }
                }

                TempData["ErrorMessage"] = "Некоректні дані імпорту.";
                return RedirectToPage(new { SelectedGroupId, SelectedDate, BulkMode });
            case "ExportSemesterHours":
                if (Role != "Admin") return Forbid();
                return await OnPostExportSemesterAsync();

            case "ChangeTeacher":
                if (Role != "Admin") return Forbid();
                await ChangeTeacherAsync(client);

                var changed = await client.GetAsync(ApiUrl($"/api/Lesson/{InputLesson.Id}"));
                if (changed.IsSuccessStatusCode)
                {
                    var json = await changed.Content.ReadAsStringAsync();
                    var lesson = JsonSerializer.Deserialize<Lesson>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (lesson != null) LastEditedDateIso = lesson.StartTime;
                }
                return RedirectToPage(new { SelectedGroupId = InputLesson.GroupId, SelectedDate, BulkMode });

            case "AddLesson":
                if (Role != "Admin") return Forbid();
                await CreateLessonAsync(client);
                LastEditedDateIso = InputLesson.StartTime;
                return RedirectToPage(new { SelectedGroupId = InputLesson.GroupId, SelectedDate = DateOnly.FromDateTime(InputLesson.StartTime), BulkMode });

            case "EditLesson":
                if (Role != "Teacher") return Forbid();
                await UpdateTopicAsync(client);
                LastEditedDateIso = InputLesson.StartTime;
                return RedirectToPage(new { SelectedGroupId = InputLesson.GroupId, SelectedDate = DateOnly.FromDateTime(InputLesson.StartTime), BulkMode });

            case "BulkApplyChanges":
                if (Role != "Admin") return Forbid();
                await ApplyBulkChangesAsync();
                LastEditedDateIso = null;
                return RedirectToPage(new { SelectedGroupId, SelectedDate, BulkMode = false });

            case "Export":
                return await OnPostExportAsync();

            default:
                return RedirectToPage(new { SelectedGroupId, SelectedDate });
        }
    }
    private async Task<IActionResult> OnPostExportSemesterAsync()
    {
        var token = Request.Cookies["cookies"];
        if (string.IsNullOrEmpty(token))
            return Unauthorized();

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var dto = new
        {
            Year = ExportSemester.Year,
            Semester = ExportSemester.Semester
        };

        var content = new StringContent(
            JsonSerializer.Serialize(dto),
            Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync(
            ApiUrl("/api/Lesson/export/semester"),
            content);

        if (!response.IsSuccessStatusCode)
        {
            TempData["ErrorMessage"] =
                "Не вдалося сформувати звіт за семестр.";
            return RedirectToPage(new
            {
                SelectedGroupId,
                SelectedDate,
                BulkMode
            });
        }

        var fileBytes = await response.Content.ReadAsByteArrayAsync();

        var contentType =
            response.Content.Headers.ContentType?.MediaType
            ?? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        var fileName =
            response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName
            ?? $"Semester_{ExportSemester.Year}_{ExportSemester.Semester}.xlsx";

        return File(
            fileBytes,
            contentType,
            fileName.Trim('"'));
    }
    
    private async Task ApplyBulkChangesAsync()
    {
        var token = Request.Cookies["cookies"];
        if (string.IsNullOrEmpty(token)) return;

        // 1) Забираем изменения из формы
        var changes = JsonSerializer.Deserialize<List<BulkChangeDto>>(
            BulkChangesJson ?? "[]",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        ) ?? new();
        if (changes.Count == 0) return;

        // 2) Определяем диапазон
        var startDate = changes.Min(c => c.StartDate).Date;
        var endDate = BulkEndDate.Date;
        if (endDate < startDate)
        {
            TempData["ErrorMessage"] = "Кінцева дата раніше за початкову.";
            return;
        }

        // 3) Подтягиваем уроки базового дня для текущей группы — чтобы иметь StartTime/GroupId и текущие значения полей
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var dayUrl = ApiUrl($"/api/Lesson/group/{SelectedGroupId}/date/{startDate:yyyy-MM-dd}");
        var resp = await client.GetAsync(dayUrl);
        if (!resp.IsSuccessStatusCode) return;

        var json = await resp.Content.ReadAsStringAsync();
        var dayLessons = JsonSerializer.Deserialize<List<Lesson>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

        var baselineById = dayLessons.ToDictionary(l => l.Id, l => l);

        // 4) Собираем payload из BulkApplyLessonDto, с ФОЛБЭКОМ по Id
        var payloadLessons = new List<BulkApplyLessonDto>();

        foreach (var ch in changes)
        {
            Lesson? baseLesson;
            if (!baselineById.TryGetValue(ch.Id, out baseLesson))
            {
                // Фикс: если Id не из базового дня — добираем урок напрямую по Id
                var byIdResp = await client.GetAsync(ApiUrl($"/api/Lesson/{ch.Id}"));
                if (!byIdResp.IsSuccessStatusCode) continue;

                var js = await byIdResp.Content.ReadAsStringAsync();
                baseLesson = JsonSerializer.Deserialize<Lesson>(js, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (baseLesson == null) continue;
            }

            var dto = new BulkApplyLessonDto
            {
                Id = baseLesson.Id,
                GroupId = baseLesson.GroupId,
                // Важен TimeOfDay слота — контроллер сопоставляет по нему
                StartTime = baseLesson.StartTime,

                // Если Delete=true — значения ниже игнорируются контроллером, но можно слать
                Name = ch.Name ?? baseLesson.Name,
                Topic = ch.Topic ?? baseLesson.Topic,
                Homework = ch.Homework ?? baseLesson.Homework,
                Clocks = ch.Clocks.HasValue ? ch.Clocks : baseLesson.Clocks,
                TeacherId = ch.TeacherId ?? baseLesson.TeacherId,
                SecondTeacherId = baseLesson.SecondTeacherId,

                Delete = ch.Delete == true
            };

            payloadLessons.Add(dto);
        }

        if (payloadLessons.Count == 0) return;

        var payload = new
        {
            Lessons = payloadLessons,
            StartDate = startDate,
            EndDate = endDate
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        await client.PostAsync(ApiUrl("/api/Lesson/bulk-apply"), content);
    }

    private async Task ChangeTeacherAsync(HttpClient client)
    {
        var getResponse = await client.GetAsync(ApiUrl($"/api/Lesson/{InputLesson.Id}"));
        if (!getResponse.IsSuccessStatusCode) return;

        var json = await getResponse.Content.ReadAsStringAsync();
        var lesson = JsonSerializer.Deserialize<Lesson>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (lesson == null) return;

        lesson.TeacherId = InputLesson.TeacherId;

        var updatedJson = JsonSerializer.Serialize(lesson);
        var content = new StringContent(updatedJson, Encoding.UTF8, "application/json");
        await client.PutAsync(ApiUrl($"/api/Lesson/{lesson.Id}"), content);
    }

    private async Task ImportLessonsAsync(
    HttpClient client,
    IFormFile file,
    Guid groupId,
    bool isNumerator,
    DateTime startDate,
    DateTime endDate)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(file.OpenReadStream()), "File", file.FileName);
        content.Add(new StringContent(groupId.ToString()), "GroupId");
        content.Add(new StringContent(isNumerator.ToString()), "IsNumerator");

        // НОВЕ: діапазон імпорту
        content.Add(new StringContent(startDate.ToString("yyyy-MM-dd")), "StartDate");
        content.Add(new StringContent(endDate.ToString("yyyy-MM-dd")), "EndDate");

        await client.PostAsync(ApiUrl("/api/Lesson/import"), content);
    }

    private async Task<IActionResult> OnPostExportAsync()
    {
        var token = Request.Cookies["cookies"];
        if (string.IsNullOrEmpty(token)) return Unauthorized();

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var query = new List<string>
        {
            $"teacherId={ExportFilters.TeacherId}",
            $"startDate={ExportFilters.StartDate:yyyy-MM-dd}",
            $"endDate={ExportFilters.EndDate:yyyy-MM-dd}"
        };
        if (ExportFilters.GroupId.HasValue)
            query.Add($"groupId={ExportFilters.GroupId.Value}");
        if (!string.IsNullOrWhiteSpace(ExportFilters.SubjectName))
            query.Add($"subjectName={Uri.EscapeDataString(ExportFilters.SubjectName)}");

        var requestUrl = ApiUrl($"/api/Lesson/export?{string.Join("&", query)}");
        var response = await client.GetAsync(requestUrl);

        if (!response.IsSuccessStatusCode)
        {
            TempData["ErrorMessage"] = "Не вдалося сформувати звіт. Перевірте параметри або спробуйте пізніше.";
            return RedirectToPage();
        }

        var fileBytes = await response.Content.ReadAsByteArrayAsync();
        var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                          ?? response.Content.Headers.ContentDisposition?.FileName
                          ?? $"Export_{DateTime.Now:yyyy-MM-dd}.xlsx";

        return File(fileBytes, contentType, fileName);
    }

    private async Task CreateLessonAsync(HttpClient client)
    {
        var lesson = new
        {
            Id = Guid.NewGuid(),
            GroupId = InputLesson.GroupId,
            TeacherId = InputLesson.TeacherId,
            Name = InputLesson.Name,
            Topic = InputLesson.Topic,
            Homework = InputLesson.Homework,
            StartTime = InputLesson.StartTime,
            Subject = "доданий вручну",

            RepeatWeekly = InputLesson.RepeatWeekly,
            EndDate = InputLesson.EndDate?.Date,

            ForNumerator = InputLesson.ForNumerator == 1 ? 1 : 0,
            ForDenominator = InputLesson.ForDenominator == 1 ? 1 : 0
        };

        var json = JsonSerializer.Serialize(lesson);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        await client.PostAsync(ApiUrl("/api/Lesson"), content);

    }

    private async Task UpdateTopicAsync(HttpClient client)
    {
        var getResponse = await client.GetAsync(ApiUrl($"/api/Lesson/{InputLesson.Id}"));
        if (!getResponse.IsSuccessStatusCode) return;

        var json = await getResponse.Content.ReadAsStringAsync();
        var lesson = JsonSerializer.Deserialize<Lesson>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (lesson == null) return;

        lesson.Topic = InputLesson.Topic;
        lesson.Homework = InputLesson.Homework;
        lesson.Clocks = InputLesson.Clocks;

        var updatedJson = JsonSerializer.Serialize(lesson);
        var content = new StringContent(updatedJson, Encoding.UTF8, "application/json");
        await client.PutAsync(ApiUrl($"/api/Lesson/{lesson.Id}"), content);
    }

    private List<DateOnly> GetWeekDays(DateOnly date)
    {
        var monday = date.AddDays(-(int)date.DayOfWeek + (date.DayOfWeek == DayOfWeek.Sunday ? -6 : 1));
        return Enumerable.Range(0, 7).Select(i => monday.AddDays(i)).ToList();
    }

    private async Task PreloadTeacherNames()
    {
        foreach (var lesson in DayLessons)
        {
            await GetTeacherNameAsync(lesson.TeacherId);
            if (lesson.SecondTeacherId.HasValue && lesson.SecondTeacherId.Value != Guid.Empty)
                await GetTeacherNameAsync(lesson.SecondTeacherId.Value);
        }
    }

    public async Task<string> GetTeacherNameAsync(Guid teacherId)
    {
        if (_teacherNameCache.TryGetValue(teacherId, out var cachedName))
            return cachedName;

        var token = Request.Cookies["cookies"];
        if (string.IsNullOrEmpty(token)) return "Unauthorized";

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync(ApiUrl($"/api/User/teacher/{teacherId}"));
        if (!response.IsSuccessStatusCode) return "—";

        var json = await response.Content.ReadAsStringAsync();
        var teacher = JsonSerializer.Deserialize<User>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var name = teacher?.FullName ?? "—";

        _teacherNameCache[teacherId] = name;
        return name;
    }

    private string ApiUrl(string relativePath)
    {
        var path = relativePath.StartsWith("/") ? relativePath : "/" + relativePath;
        return $"{Request.Scheme}://{Request.Host}{path}";
    }

    // ---- Формы ----
    public class ExportFormDto
    {
        [BindProperty] public Guid TeacherId { get; set; }
        [BindProperty] public DateTime StartDate { get; set; }
        [BindProperty] public DateTime EndDate { get; set; }
        [BindProperty] public Guid? GroupId { get; set; }
        [BindProperty] public string? SubjectName { get; set; }
    }

    public class LessonFormDto
    {
        public Guid Id { get; set; }
        public Guid GroupId { get; set; }
        public Guid TeacherId { get; set; }
        public string Name { get; set; } = "";
        public DateTime StartTime { get; set; }
        public string? Topic { get; set; } = "";
        public string? Homework { get; set; } = "";
        public int? Clocks { get; set; }
        public bool RepeatWeekly { get; set; }       // чекбокс серії
        public DateTime? EndDate { get; set; }       // дата завершення серії (тільки дата)
        public int ForNumerator { get; set; } = 0;
        public int ForDenominator { get; set; } = 0;


    }
}