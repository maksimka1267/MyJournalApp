using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using MyJournalApp.Data.Models;
using System.Net.Http;
using System.Net.Http.Headers;

public class UsersModel : PageModel
{
    private readonly HttpClient _httpClient;

    public UsersModel(IHttpClientFactory factory)
    {
        _httpClient = factory.CreateClient("ApiClient");
    }

    public List<User> Users { get; set; } = new();
    public List<Group> Groups { get; set; } = new();

    [BindProperty(SupportsGet = true)] public string SearchTerm { get; set; }
    [BindProperty(SupportsGet = true)] public string SelectedRole { get; set; }
    [BindProperty]
    public Guid TeacherId { get; set; }

    [BindProperty]
    public bool IsAdminFlag { get; set; }
    public Dictionary<Guid, bool> TeacherAdminStatus { get; set; } = new();


    // Новые модели
    [BindProperty] public CreateUserModel NewUser { get; set; }
    [BindProperty] public UploadExcelModel ExcelUpload { get; set; }

    public IEnumerable<User> FilteredUsers =>
        Users.Where(u =>
            (string.IsNullOrEmpty(SearchTerm) || u.FullName.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrEmpty(SelectedRole) || u.Role == SelectedRole));

    public async Task<IActionResult> OnGetAsync(string? search, string? role)
    {
        var token = Request.Cookies["cookies"];
        if (string.IsNullOrEmpty(token)) return RedirectToPage("/Account/Login");

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        SearchTerm = search ?? string.Empty;
        SelectedRole = role ?? string.Empty;

        var query = new List<string>();
        if (!string.IsNullOrEmpty(SearchTerm))
            query.Add($"search={SearchTerm}");
        if (!string.IsNullOrEmpty(SelectedRole))
            query.Add($"role={SelectedRole}");

        var queryString = query.Any() ? "?" + string.Join("&", query) : string.Empty;

        Users = await _httpClient.GetFromJsonAsync<List<User>>($"api/User/users{queryString}") ?? new();
        Groups = await _httpClient.GetFromJsonAsync<List<Group>>("api/Group/all") ?? new();
        var teachers = await _httpClient.GetFromJsonAsync<List<Teacher>>("api/User/teachers-admin-status") ?? new();
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
        var token = Request.Cookies["cookies"];
        if (string.IsNullOrEmpty(token)) return RedirectToPage("/Account/Login");

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (ExcelUpload != null && ExcelUpload.File != null && ExcelUpload.File.Length > 0)
        {
            await UploadExcelAsync();
            return RedirectToPage();
        }
        if (TeacherId != Guid.Empty)
        {
            await UpdateTeacherAdminStatusAsync();
            return RedirectToPage();
        }
        // 3️⃣ Создание нового пользователя
        if (NewUser != null && !string.IsNullOrEmpty(NewUser.Email))
        {
            await CreateUserAsync();
            return RedirectToPage();
        }
        // 1️⃣ Удаление пользователей
        if (id.HasValue && id != Guid.Empty)
        {
         var response = await _httpClient.DeleteAsync($"api/User/delete/{id}");
         if (!response.IsSuccessStatusCode)
            ModelState.AddModelError(string.Empty, "Не вдалося видалити користувача.");
        }
        else
        {
            await _httpClient.DeleteAsync($"api/User/delete-all");
        }
        return RedirectToPage();
    }
    private async Task UpdateTeacherAdminStatusAsync()
    {
        var payload = new { TeacherId, IsAdmin = IsAdminFlag };
        var response = await _httpClient.PutAsJsonAsync("api/User/update-teacher-admin", payload);

        if (!response.IsSuccessStatusCode)
            ModelState.AddModelError(string.Empty, "Не вдалося змінити статус адміністратора.");
    }

    private async Task UploadExcelAsync()
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(ExcelUpload.File.OpenReadStream()), "File", ExcelUpload.File.FileName);
        content.Add(new StringContent(ExcelUpload.Role), "Role");
        if (ExcelUpload.GroupId.HasValue)
            content.Add(new StringContent(ExcelUpload.GroupId.Value.ToString()), "GroupId");

        var response = await _httpClient.PostAsync("api/Auth/bulk-register", content);
        if (!response.IsSuccessStatusCode)
            ModelState.AddModelError(string.Empty, "Помилка при завантаженні Excel.");
    }

    private async Task CreateUserAsync()
    {
        var content = new MultipartFormDataContent
        {
            { new StringContent(NewUser.FullName), "FullName" },
            { new StringContent(NewUser.Email), "Email" },
            { new StringContent(NewUser.Password), "Password" },
            { new StringContent(NewUser.Role), "Role" }
        };

        if (NewUser.GroupId.HasValue)
            content.Add(new StringContent(NewUser.GroupId.Value.ToString()), "GroupId");

        var response = await _httpClient.PostAsync("api/Auth/register", content);
        if (!response.IsSuccessStatusCode)
            ModelState.AddModelError(string.Empty, "Помилка при створенні користувача.");
    }
    public class CreateUserModel
    {
        public string FullName { get; set; }

        public string Email { get; set; }

        public string Password { get; set; }

        public string Role { get; set; }
        public Guid? GroupId { get; set; }
    }
    public class UploadExcelModel
    {
        public IFormFile File { get; set; }
        public string Role { get; set; }
        public Guid? GroupId { get; set; }
    }

}
