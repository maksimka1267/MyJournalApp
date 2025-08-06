using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyJournalApp.Data.Models;
using System.Net.Http.Headers;
using System.Net.Http.Json;

[Authorize]
public class StudentsModel : PageModel
{
    private readonly HttpClient _httpClient;

    public StudentsModel(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("ApiClient");
    }

    public List<StudentViewModel> Students { get; set; } = new();
    public List<GroupSummary> GroupSummaries { get; set; } = new();
    public Dictionary<Guid, string> GroupNames { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {

        var token = Request.Cookies["cookies"];
        if (string.IsNullOrEmpty(token)) return RedirectToPage("/Account/Login");

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var users = await _httpClient.GetFromJsonAsync<List<User>>("api/User/users") ?? new();
        var students = await _httpClient.GetFromJsonAsync<List<User>>("api/User/students") ?? new();
        var groups = await _httpClient.GetFromJsonAsync<List<Group>>("api/Group/all") ?? new();
        TempData["Debug"] = $"Loaded {students.Count} students";

        GroupNames = groups.ToDictionary(g => g.Id, g => g.Name);

        // карточки групп
        foreach (var group in groups)
        {
            int count = group.StudentIds?.Count ?? 0;
            GroupSummaries.Add(new GroupSummary
            {
                Id = group.Id,
                Name = group.Name,
                StudentCount = count
            });
        }

        // таблица студентов
        foreach (var student in students)
        {
            var group = groups.FirstOrDefault(g => g.StudentIds?.Contains(student.Id) == true);
            Students.Add(new StudentViewModel
            {
                Id = student.Id,
                FullName = student.FullName,
                Email = student.Email,
                GroupName = group?.Name ?? "Ч"
            });
        }

        return Page();
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
