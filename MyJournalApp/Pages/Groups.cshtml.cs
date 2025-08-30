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
        _httpClient = httpClientFactory.CreateClient(); // без BaseAddress
    }

    public List<GroupWithDetails> GroupsWithDetails { get; set; } = new();
    public List<User> AllTeachers { get; set; } = new();
    public Dictionary<Guid, string> TeacherNames { get; set; } = new();

    [BindProperty] public GroupExcelImportModel ExcelImport { get; set; }
    [BindProperty] public MoveStudentModel MoveStudent { get; set; }
    [BindProperty] public string GroupName { get; set; } = "";
    [BindProperty] public Guid TeacherId { get; set; }
    [BindProperty] public ReportRequestModel Report { get; set; }

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

        var me = await _httpClient.GetFromJsonAsync<User>(ApiUrl("/api/Auth/me"));
        if (me == null) return RedirectToPage("/Account/Login");

        var groups = me.Role switch
        {
            "Admin" => await _httpClient.GetFromJsonAsync<List<Group>>(ApiUrl("/api/Group/all")) ?? new(),
            "Teacher" => await _httpClient.GetFromJsonAsync<List<Group>>(ApiUrl("/api/Group/my")) ?? new(),
            "Student" => await _httpClient.GetFromJsonAsync<List<Group>>(ApiUrl("/api/Group/student")) ?? new(),
            _ => new()
        };

        AllTeachers = await _httpClient.GetFromJsonAsync<List<User>>(ApiUrl("/api/User/teachers")) ?? new();
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

            var students = await _httpClient.GetFromJsonAsync<List<User>>(ApiUrl($"/api/Group/{group.Id}/users"));
            groupDetails.Students = students ?? new();

            GroupsWithDetails.Add(groupDetails);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string handler, Guid id)
    {
        var token = Request.Cookies["cookies"];
        if (string.IsNullOrEmpty(token))
            return RedirectToPage("/Account/Login");

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return handler switch
        {
            "ImportExcel" => await ImportGroupsFromExcelAsync(),
            "MoveStudent" => await MoveStudentAsync(),
            "CreateGroup" => await CreateGroupAsync(),
            "GenerateReport" => await GenerateReportAsync(),
            "DeleteGroup" => await DeleteGroupAsync(id),
            _ => Page()
        };
    }
    private async Task<IActionResult> DeleteGroupAsync(Guid id)
    {
        if (id == Guid.Empty)
        {
            TempData["Error"] = "Невірний ідентифікатор групи.";
            return RedirectToPage();
        }

        var resp = await _httpClient.DeleteAsync(ApiUrl($"/api/Group/{id}"));
        if (!resp.IsSuccessStatusCode)
        {
            // Можно прочитать причину: var text = await resp.Content.ReadAsStringAsync();
            TempData["Error"] = "Не вдалося видалити групу. Переконайтесь, що у вас є права, і спробуйте ще раз.";
            return RedirectToPage();
        }

        TempData["Success"] = "Групу видалено.";
        return RedirectToPage();
    }
    private async Task<IActionResult> ImportGroupsFromExcelAsync()
    {
        if (ExcelImport?.File == null || ExcelImport.File.Length == 0)
            return Page();

        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(ExcelImport.File.OpenReadStream()), "File", ExcelImport.File.FileName);

        var resp = await _httpClient.PostAsync(ApiUrl("/api/Group/bulk-import"), content);
        if (!resp.IsSuccessStatusCode)
            TempData["Error"] = "Помилка при імпорті груп з Excel.";

        return RedirectToPage();
    }

    private async Task<IActionResult> MoveStudentAsync()
    {
        if (MoveStudent == null ||
            MoveStudent.StudentId == Guid.Empty ||
            MoveStudent.FromGroupId == Guid.Empty ||
            MoveStudent.ToGroupId == Guid.Empty)
            return Page();

        var resp = await _httpClient.PutAsJsonAsync(ApiUrl("/api/Group/move-student"), MoveStudent);
        if (!resp.IsSuccessStatusCode)
            TempData["Error"] = "Не вдалося перемістити студента.";

        return RedirectToPage();
    }

    private async Task<IActionResult> CreateGroupAsync()
    {
        if (string.IsNullOrWhiteSpace(GroupName) || TeacherId == Guid.Empty)
            return Page();

        var newGroup = new Group
        {
            Id = Guid.NewGuid(),
            Name = GroupName,
            TeacherId = TeacherId
        };

        var resp = await _httpClient.PostAsJsonAsync(ApiUrl("/api/Group"), newGroup);
        if (!resp.IsSuccessStatusCode)
        {
            TempData["Error"] = "Не вдалося створити групу.";
            return Page();
        }

        return RedirectToPage();
    }

    private async Task<IActionResult> GenerateReportAsync()
    {
        if (Report == null || Report.GroupId == Guid.Empty || Report.StartDate == default || Report.EndDate == default)
            return Page();

        var url = ApiUrl($"/api/Report/absences/group/{Report.GroupId}?startDate={Report.StartDate:yyyy-MM-dd}&endDate={Report.EndDate:yyyy-MM-dd}");
        var resp = await _httpClient.GetAsync(url);

        if (!resp.IsSuccessStatusCode)
        {
            TempData["Error"] = "Не вдалося згенерувати рапортичку.";
            return RedirectToPage();
        }

        var disposition = resp.Content.Headers.ContentDisposition;
        var fileName = disposition?.FileNameStar ?? disposition?.FileName ?? $"Рапортичка_{Report.GroupId}.xlsx";

        var bytes = await resp.Content.ReadAsByteArrayAsync();
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    private string ApiUrl(string relativePath)
    {
        var path = relativePath.StartsWith("/") ? relativePath : "/" + relativePath;
        return $"{Request.Scheme}://{Request.Host}{path}";
    }
}

// Модели
public class GroupExcelImportModel
{
    public IFormFile File { get; set; }
}

public class MoveStudentModel
{
    public Guid StudentId { get; set; }
    public Guid FromGroupId { get; set; }
    public Guid ToGroupId { get; set; }
}

public class ReportRequestModel
{
    public Guid GroupId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}
