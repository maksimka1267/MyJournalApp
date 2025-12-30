using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyJournalApp.Data.Models;
using System.Net.Http.Headers;
using System.Net.Http.Json;

[Authorize]
public class StudentsModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public StudentsModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public List<StudentViewModel> Students { get; set; } = new();
    public List<GroupSummary> GroupSummaries { get; set; } = new();
    public Dictionary<Guid, string> GroupNames { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        // 1) JWT из куки
        if (!Request.Cookies.TryGetValue("cookies", out var jwt) || string.IsNullOrWhiteSpace(jwt))
            return RedirectToPage("/Account/Login");

        // 2) HttpClient без BaseAddress
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        // 3) Параллельно тянем данные
        var usersTask = client.GetFromJsonAsync<List<User>>(ApiUrl("/api/User/users"));
        var studentsTask = client.GetFromJsonAsync<List<User>>(ApiUrl("/api/User/students"));
        var groupsTask = client.GetFromJsonAsync<List<Group>>(ApiUrl("/api/Group/all"));

        await Task.WhenAll(usersTask!, studentsTask!, groupsTask!);

        var users = usersTask?.Result ?? new();
        var students = studentsTask?.Result ?? new();
        var groups = groupsTask?.Result ?? new();

        TempData["Debug"] = $"Loaded {students.Count} students";

        // 4) Справочники
        GroupNames = groups.ToDictionary(g => g.Id, g => g.Name);

        // 5) Карточки групп
        GroupSummaries = groups
            .Select(g => new GroupSummary
            {
                Id = g.Id,
                Name = g.Name,
                StudentCount = g.StudentIds?.Count ?? 0
            })
            .ToList();

        // 6) Таблица студентов
        var groupByStudent = groups
            .SelectMany(g => (g.StudentIds ?? new List<Guid>()).Select(id => new { StudentId = id, GroupName = g.Name }))
            .ToDictionary(x => x.StudentId, x => x.GroupName);

        Students = students.Select(s => new StudentViewModel
        {
            Id = s.Id,
            FullName = s.FullName,
            Email = s.Email,
            GroupName = groupByStudent.TryGetValue(s.Id, out var name) ? name : "—"
        }).ToList();

        return Page();
    }

    // Абсолютный URL к API на текущем домене
    private string ApiUrl(string relativePath)
    {
        var path = relativePath.StartsWith("/") ? relativePath : "/" + relativePath;
        return $"{Request.Scheme}://{Request.Host}{path}";
    }

    public class StudentViewModel
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string GroupName { get; set; } = "";
    }

    public class GroupSummary
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public int StudentCount { get; set; }
    }
}
