using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyJournalApp.Data.Models;
using System.Net.Http.Headers;
using System.Net.Http.Json;

[Authorize]
public class GroupsModel : PageModel
{
    private readonly HttpClient _httpClient;

    public GroupsModel(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("ApiClient");
    }

    public List<GroupWithDetails> GroupsWithDetails { get; set; } = new();
    public List<User> AllTeachers { get; set; } = new();
    public Dictionary<Guid, string> TeacherNames { get; set; } = new();

    // 📌 Модель для импорта Excel
    [BindProperty] public GroupExcelImportModel ExcelImport { get; set; }

    // 📌 Модель перемещения студента
    [BindProperty] public MoveStudentModel MoveStudent { get; set; }

    // 📌 Для создания группы
    [BindProperty] public string GroupName { get; set; } = "";
    [BindProperty] public Guid TeacherId { get; set; }

    public class GroupWithDetails
    {
        public Group Group { get; set; } = null!;
        public string TeacherName { get; set; } = "Невідомий";
        public string TeacherEmail { get; set; } = "невідома";
        public List<User> Students { get; set; } = new();
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var token = Request.Cookies["cookies"];
        if (string.IsNullOrEmpty(token))
            return RedirectToPage("/Account/Login");

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var me = await _httpClient.GetFromJsonAsync<User>("api/Auth/me");
        if (me == null) return RedirectToPage("/Account/Login");

        var groups = me.Role switch
        {
            "Admin" => await _httpClient.GetFromJsonAsync<List<Group>>("api/Group/all") ?? new(),
            "Teacher" => await _httpClient.GetFromJsonAsync<List<Group>>("api/Group/my") ?? new(),
            "Student" => await _httpClient.GetFromJsonAsync<List<Group>>("api/Group/student") ?? new(),
            _ => new()
        };

        AllTeachers = await _httpClient.GetFromJsonAsync<List<User>>("api/User/teachers") ?? new();
        TeacherNames = AllTeachers.ToDictionary(
            t => t.Id,
            t => string.IsNullOrWhiteSpace(t.FullName) ? "Без імені" : t.FullName
        );

        foreach (var group in groups)
        {
            var groupDetails = new GroupWithDetails { Group = group };

            var teacher = AllTeachers.FirstOrDefault(t => t.Id == group.TeacherId);
            if (teacher != null)
            {
                groupDetails.TeacherName = teacher.FullName;
                groupDetails.TeacherEmail = teacher.Email;
            }

            var students = await _httpClient.GetFromJsonAsync<List<User>>($"api/Group/{group.Id}/users");
            groupDetails.Students = students ?? new();

            GroupsWithDetails.Add(groupDetails);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var token = Request.Cookies["cookies"];
        if (string.IsNullOrEmpty(token))
            return RedirectToPage("/Account/Login");

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 1️⃣ Импорт Excel
        if (ExcelImport != null && ExcelImport.File != null && ExcelImport.File.Length > 0)
        {
            await ImportGroupsFromExcelAsync();
            return RedirectToPage();
        }

        // 2️⃣ Перемещение студента
        if (MoveStudent != null && MoveStudent.StudentId != Guid.Empty &&
            MoveStudent.FromGroupId != Guid.Empty && MoveStudent.ToGroupId != Guid.Empty)
        {
            await MoveStudentAsync();
            return RedirectToPage();
        }

        // 3️⃣ Создание группы
        var newGroup = new Group
        {
            Id = Guid.NewGuid(),
            Name = GroupName,
            TeacherId = TeacherId
        };

        var response = await _httpClient.PostAsJsonAsync("api/Group", newGroup);
        if (!response.IsSuccessStatusCode)
        {
            TempData["Error"] = "Не вдалося створити групу.";
            return Page();
        }

        return RedirectToPage();
    }

    // 📌 Локальный метод для Excel импорта
    private async Task ImportGroupsFromExcelAsync()
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(ExcelImport.File.OpenReadStream()), "File", ExcelImport.File.FileName);

        var response = await _httpClient.PostAsync("api/Group/bulk-import", content);
        if (!response.IsSuccessStatusCode)
            TempData["Error"] = "Помилка при імпорті груп з Excel.";
    }

    // 📌 Локальный метод перемещения студента
    private async Task MoveStudentAsync()
    {
        var response = await _httpClient.PutAsJsonAsync("api/Group/move-student", MoveStudent);
        if (!response.IsSuccessStatusCode)
            TempData["Error"] = "Не вдалося перемістити студента.";
    }
}

// 📌 Модель для импорта Excel
public class GroupExcelImportModel
{
    public IFormFile File { get; set; }
}

// 📌 Модель перемещения студента
public class MoveStudentModel
{
    public Guid StudentId { get; set; }
    public Guid FromGroupId { get; set; }
    public Guid ToGroupId { get; set; }
}
