using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyJournalApp.Data.Models;
using System.Net.Http.Headers;
using System.Net.Http.Json;

public class UsersModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public UsersModel(IHttpClientFactory factory)
    {
        _httpClientFactory = factory;
    }

    public List<User> Users { get; set; } = new();
    public List<Group> Groups { get; set; } = new();
    public Dictionary<Guid, bool> TeacherAdminStatus { get; set; } = new();

    [BindProperty(SupportsGet = true)] public string SearchTerm { get; set; } = string.Empty;
    [BindProperty(SupportsGet = true)] public string SelectedRole { get; set; } = string.Empty;

    [BindProperty] public Guid TeacherId { get; set; }
    [BindProperty] public bool IsAdminFlag { get; set; }

    // Новые модели
    [BindProperty] public CreateUserModel NewUser { get; set; } = new();
    [BindProperty] public UploadExcelModel ExcelUpload { get; set; } = new();

    public IEnumerable<User> FilteredUsers =>
        Users.Where(u =>
            (string.IsNullOrEmpty(SearchTerm) || u.FullName.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrEmpty(SelectedRole) || u.Role == SelectedRole));

    public async Task<IActionResult> OnGetAsync(string? search, string? role)
    {
        if (!Request.Cookies.TryGetValue("cookies", out var jwt) || string.IsNullOrWhiteSpace(jwt))
            return RedirectToPage("/Account/Login");

        SearchTerm = search ?? string.Empty;
        SelectedRole = role ?? string.Empty;

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        // querystring
        var qs = new List<string>();
        if (!string.IsNullOrEmpty(SearchTerm)) qs.Add($"search={Uri.EscapeDataString(SearchTerm)}");
        if (!string.IsNullOrEmpty(SelectedRole)) qs.Add($"role={Uri.EscapeDataString(SelectedRole)}");
        var query = qs.Count > 0 ? "?" + string.Join("&", qs) : string.Empty;

        // параллельные запросы
        var usersTask = client.GetFromJsonAsync<List<User>>(ApiUrl($"/api/User/users{query}"));
        var groupsTask = client.GetFromJsonAsync<List<Group>>(ApiUrl("/api/Group/all"));
        var teachersTask = client.GetFromJsonAsync<List<Teacher>>(ApiUrl("/api/User/teachers-admin-status"));

        await Task.WhenAll(usersTask!, groupsTask!, teachersTask!);

        Users = usersTask?.Result ?? new();
        Groups = groupsTask?.Result ?? new();
        var teachers = teachersTask?.Result ?? new();

        TeacherAdminStatus = teachers.ToDictionary(t => t.Id, t => t.IsAdmin);

        return Page();
    }

    public string GetGroupName(Guid userId, string role)
    {
        if (role == "Student")
            return Groups.FirstOrDefault(g => g.StudentIds?.Contains(userId) == true)?.Name ?? "-";
        if (role == "Teacher")
            return Groups.FirstOrDefault(g => g.TeacherId == userId)?.Name ?? "-";
        return "-";
    }

    public async Task<IActionResult> OnPostAsync(Guid? id)
    {
        if (!Request.Cookies.TryGetValue("cookies", out var jwt) || string.IsNullOrWhiteSpace(jwt))
            return RedirectToPage("/Account/Login");

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        // 1) Импорт из Excel
        if (ExcelUpload?.File is not null && ExcelUpload.File.Length > 0)
        {
            using var content = new MultipartFormDataContent();
            content.Add(new StreamContent(ExcelUpload.File.OpenReadStream()), "File", ExcelUpload.File.FileName);
            content.Add(new StringContent(ExcelUpload.Role ?? string.Empty), "Role");
            if (ExcelUpload.GroupId.HasValue)
                content.Add(new StringContent(ExcelUpload.GroupId.Value.ToString()), "GroupId");

            var resp = await client.PostAsync(ApiUrl("/api/Auth/bulk-register"), content);
            if (!resp.IsSuccessStatusCode)
                ModelState.AddModelError(string.Empty, "Помилка при завантаженні Excel.");

            return RedirectToPage();
        }

        // 2) Обновление статуса админа у викладача
        if (TeacherId != Guid.Empty)
        {
            var payload = new { TeacherId, IsAdmin = IsAdminFlag };
            var resp = await client.PutAsJsonAsync(ApiUrl("/api/User/update-teacher-admin"), payload);
            if (!resp.IsSuccessStatusCode)
                ModelState.AddModelError(string.Empty, "Не вдалося змінити статус адміністратора.");

            return RedirectToPage();
        }

        // 3) Создание нового пользователя
        if (!string.IsNullOrWhiteSpace(NewUser?.Email))
        {
            using var content = new MultipartFormDataContent
            {
                { new StringContent(NewUser.FullName ?? string.Empty), "FullName" },
                { new StringContent(NewUser.Email), "Email" },
                { new StringContent(NewUser.Password ?? string.Empty), "Password" },
                { new StringContent(NewUser.Role ?? string.Empty), "Role" }
            };
            if (NewUser.GroupId.HasValue)
                content.Add(new StringContent(NewUser.GroupId.Value.ToString()), "GroupId");

            var resp = await client.PostAsync(ApiUrl("/api/Auth/register"), content);
            if (!resp.IsSuccessStatusCode)
                ModelState.AddModelError(string.Empty, "Помилка при створенні користувача.");

            return RedirectToPage();
        }

        // 4) Удаление (конкретного или всех)
        if (id.HasValue && id.Value != Guid.Empty)
        {
            var resp = await client.DeleteAsync(ApiUrl($"/api/User/delete/{id}"));
            if (!resp.IsSuccessStatusCode)
                ModelState.AddModelError(string.Empty, "Не вдалося видалити користувача.");
        }
        else
        {
            await client.DeleteAsync(ApiUrl("/api/User/delete-all"));
        }

        return RedirectToPage();
    }

    private string ApiUrl(string relativePath)
    {
        var path = relativePath.StartsWith("/") ? relativePath : "/" + relativePath;
        return $"{Request.Scheme}://{Request.Host}{path}";
    }

    public class CreateUserModel
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public Guid? GroupId { get; set; }
    }

    public class UploadExcelModel
    {
        public IFormFile? File { get; set; }
        public string Role { get; set; } = string.Empty;
        public Guid? GroupId { get; set; }
    }
}
