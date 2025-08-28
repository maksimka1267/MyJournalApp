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
    public List<Group> AllGroups { get; set; } = new();
    public List<Lesson> DayLessons { get; set; } = new();
    public List<User> Teachers { get; set; } = new();
    public Teacher? CurrentTeacher { get; set; }
    public List<DateOnly> WeekDays { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public Guid SelectedGroupId { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly SelectedDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

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
            if (Guid.TryParse(userId, out var teacherId))
            {
                var response = await client.GetAsync(ApiUrl("/api/Lesson"));
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var allLessons = JsonSerializer.Deserialize<List<Lesson>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                    DayLessons = allLessons.Where(l => l.TeacherId == teacherId && DateOnly.FromDateTime(l.StartTime.Date) == SelectedDate).ToList();
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
                    if (Guid.TryParse(groupIdRaw, out var groupId) && bool.TryParse(isNumeratorRaw, out var isNumerator))
                    {
                        await ImportLessonsAsync(client, file, groupId, isNumerator);
                        return RedirectToPage(new { SelectedGroupId = groupId, SelectedDate });
                    }
                }
                TempData["ErrorMessage"] = "Некоректні дані імпорту.";
                return RedirectToPage(new { SelectedGroupId, SelectedDate });

            case "AddLesson":
                if (Role != "Admin") return Forbid();
                await CreateLessonAsync(client);
                return RedirectToPage(new
                {
                    SelectedGroupId = InputLesson.GroupId,
                    SelectedDate = DateOnly.FromDateTime(InputLesson.StartTime)
                });

            case "EditLesson":
                if (Role != "Teacher") return Forbid();
                await UpdateTopicAsync(client);
                return RedirectToPage(new
                {
                    SelectedGroupId = InputLesson.GroupId,
                    SelectedDate = DateOnly.FromDateTime(InputLesson.StartTime)
                });

            case "Export":
                return await OnPostExportAsync();

            default:
                return RedirectToPage(new { SelectedGroupId, SelectedDate });
        }
    }

    private async Task ImportLessonsAsync(HttpClient client, IFormFile file, Guid groupId, bool isNumerator)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(file.OpenReadStream()), "File", file.FileName);
        content.Add(new StringContent(groupId.ToString()), "GroupId");
        content.Add(new StringContent(isNumerator.ToString()), "IsNumerator");

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
            StartTime = InputLesson.StartTime
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
            await GetTeacherNameAsync(lesson.TeacherId);
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

    // DTOs
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
    }
}
