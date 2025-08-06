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

    [BindProperty]
    public LessonFormDto InputLesson { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var token = Request.Cookies["cookies"];
        if (string.IsNullOrEmpty(token)) return RedirectToPage("/Account/Login");

        var user = _contextAccessor.HttpContext?.User;
        var userId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Role = user?.FindFirst(ClaimTypes.Role)?.Value ?? "";

        var client = _httpClientFactory.CreateClient("ApiClient");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (Role == "Admin")
        {
            var groupResponse = await client.GetAsync("api/group/all");
            if (groupResponse.IsSuccessStatusCode)
            {
                var json = await groupResponse.Content.ReadAsStringAsync();
                AllGroups = JsonSerializer.Deserialize<List<Group>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            }

            var teacherResponse = await client.GetAsync("api/user/teachers");
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
                var studentResponse = await client.GetAsync($"api/user/student/{studentId}");
                if (studentResponse.IsSuccessStatusCode)
                {
                    var json = await studentResponse.Content.ReadAsStringAsync();
                    var student = JsonSerializer.Deserialize<Student>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    SelectedGroupId = student?.GroupId ?? Guid.Empty;
                }
            }
        }

        if (SelectedGroupId != Guid.Empty)
        {
            var response = await client.GetAsync($"api/lesson/group/{SelectedGroupId}/date/{SelectedDate.ToDateTime(TimeOnly.MinValue):yyyy-MM-dd}");
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
                var response = await client.GetAsync("api/lesson");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var allLessons = JsonSerializer.Deserialize<List<Lesson>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                    DayLessons = allLessons.Where(l => l.TeacherId == teacherId && DateOnly.FromDateTime(l.StartTime.Date) == SelectedDate).ToList();
                }
                var teacherResponse = await client.GetAsync($"api/user/teacher-model/{teacherId}");
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

    public async Task<IActionResult> OnPostAsync()
    {
        var token = Request.Cookies["cookies"];
        if (string.IsNullOrEmpty(token)) return RedirectToPage("/Account/Login");

        var user = _contextAccessor.HttpContext?.User;
        var role = user?.FindFirst(ClaimTypes.Role)?.Value;
        var userId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(role)) return Unauthorized();

        var client = _httpClientFactory.CreateClient("ApiClient");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (role == "Admin" && Request.Form.Files.Count > 0 && Request.Form.Files["File"] is IFormFile file)
        {
            var groupIdRaw = Request.Form["GroupId"];
            if (Guid.TryParse(groupIdRaw, out var groupId))
            {
                await ImportLessonsAsync(client, file, groupId);
                return RedirectToPage(new { SelectedGroupId = groupId });
            }
        }

        if (role == "Admin")
        {
            await CreateLessonAsync(client);
        }
        else if (role == "Teacher")
        {
            await UpdateTopicAsync(client);
        }

        return RedirectToPage(new { SelectedGroupId = InputLesson.GroupId, SelectedDate = DateOnly.FromDateTime(InputLesson.StartTime) });
    }

    private async Task ImportLessonsAsync(HttpClient client, IFormFile file, Guid groupId)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(file.OpenReadStream()), "File", file.FileName);
        content.Add(new StringContent(groupId.ToString()), "GroupId");

        await client.PostAsync("api/lesson/import", content);
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
        await client.PostAsync("api/lesson", content);
    }

    private async Task UpdateTopicAsync(HttpClient client)
    {
        var getResponse = await client.GetAsync($"api/lesson/{InputLesson.Id}");
        if (!getResponse.IsSuccessStatusCode) return;

        var json = await getResponse.Content.ReadAsStringAsync();
        var lesson = JsonSerializer.Deserialize<Lesson>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (lesson == null) return;

        lesson.Topic = InputLesson.Topic;
        lesson.Homework = InputLesson.Homework;

        var updatedJson = JsonSerializer.Serialize(lesson);
        var content = new StringContent(updatedJson, Encoding.UTF8, "application/json");
        await client.PutAsync($"api/lesson/{lesson.Id}", content);
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
        }
    }

    public async Task<string> GetTeacherNameAsync(Guid teacherId)
    {
        if (_teacherNameCache.TryGetValue(teacherId, out var cachedName))
            return cachedName;

        var token = Request.Cookies["cookies"];
        if (string.IsNullOrEmpty(token)) return "Unauthorized";

        var client = _httpClientFactory.CreateClient("ApiClient");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"api/user/teacher/{teacherId}");
        if (!response.IsSuccessStatusCode) return "—";

        var json = await response.Content.ReadAsStringAsync();
        var teacher = JsonSerializer.Deserialize<User>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var name = teacher?.FullName ?? "—";
        _teacherNameCache[teacherId] = name;
        return name;
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
    }
}
