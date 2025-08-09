using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Security.Claims;
using static AcademicProcessController;

public class CalendarModel : PageModel
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CalendarModel(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor)
    {
        _httpClient = httpClientFactory.CreateClient("ApiClient");
        _httpContextAccessor = httpContextAccessor;
    }

    public string Role { get; set; } = "";

    [BindProperty]
    public Guid SelectedGroupId { get; set; }

    public List<Group> AllGroups { get; set; } = new();
    public List<AcademicEvent> DisplayEvents { get; set; } = new(); // для OnGet

    [BindProperty]
    public List<AcademicEventDto> EditableEvents { get; set; } = new(); // для OnPost

    public async Task<IActionResult> OnGetAsync(Guid? groupId)
    {
        var token = Request.Cookies["cookies"];
        if (string.IsNullOrEmpty(token)) return RedirectToPage("/Account/Login");

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var userId = GetCurrentUserId();
        Role = GetCurrentUserRole();

        // Получить список всех групп
        AllGroups = await _httpClient.GetFromJsonAsync<List<Group>>("api/group/all");

        // Определить группу
        if (Role == "Student")
            SelectedGroupId = AllGroups.FirstOrDefault(g => g.StudentIds.Contains(userId))?.Id ?? Guid.Empty;
        else if (Role == "Teacher")
            SelectedGroupId = AllGroups.FirstOrDefault(g => g.TeacherId == userId)?.Id ?? Guid.Empty;
        else if (Role == "Admin")
            SelectedGroupId = groupId ?? AllGroups.FirstOrDefault()?.Id ?? Guid.Empty;

        if (SelectedGroupId == Guid.Empty)
            return Page(); // Группа не найдена

        // Учебный год
        var currentMonth = DateTime.Now.Month;
        var year = currentMonth >= 7 ? DateTime.Now.Year : DateTime.Now.Year - 1;

        // Получить события из API
        var existingEvents = await _httpClient.GetFromJsonAsync<List<AcademicEvent>>(
            $"api/academicprocess/{SelectedGroupId}/{year}") ?? new();

        // Заполнить недостающие недели
        var fullYearEvents = new List<AcademicEvent>();
        var firstWeekStart = new DateTime(year, 1, 1);

        for (int week = 1; week <= 52; week++)
        {
            var startDate = firstWeekStart.AddDays((week - 1) * 7);
            var endDate = startDate.AddDays(6);

            var existing = existingEvents.FirstOrDefault(e => e.WeekNumber == week);
            if (existing != null)
            {
                fullYearEvents.Add(existing);
            }
            else
            {
                fullYearEvents.Add(new AcademicEvent
                {
                    Id = Guid.NewGuid(),
                    GroupId = SelectedGroupId,
                    Year = year,
                    WeekNumber = week,
                    Month = startDate.Month,
                    Type = AcademicWeekType.Lecture,
                    StartDate = startDate,
                    EndDate = endDate
                });
            }
        }

        DisplayEvents = fullYearEvents;
        // Если мы в режиме редактирования — скопировать DisplayEvents -> EditableEvents
        if (Request.Query["edit"] == "true")
        {
            EditableEvents = DisplayEvents
                .Select(ev => new AcademicEventDto
                {
                    Id = ev.Id,
                    GroupId = ev.GroupId,
                    Year = ev.Year,
                    Month = ev.Month,
                    WeekNumber = ev.WeekNumber,
                    Type = ev.Type,
                    StartDate = ev.StartDate,
                    EndDate = ev.EndDate
                }).ToList();

            ModelState.Clear(); // сбросить кеш
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();
        Console.WriteLine($"⏳ EditableEvents.Count = {EditableEvents?.Count}");
        var token = Request.Cookies["cookies"];
        if (string.IsNullOrEmpty(token)) return RedirectToPage("/Account/Login");

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _httpClient.PutAsJsonAsync("/api/academicprocess/bulk", EditableEvents);

        if (!response.IsSuccessStatusCode)
        {
            ModelState.AddModelError(string.Empty, "Помилка при збереженні.");
            return Page();
        }

        return RedirectToPage("/Calendar", new { groupId = SelectedGroupId });
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier);
        return userIdClaim != null ? Guid.Parse(userIdClaim.Value) : Guid.Empty;
    }

    private string GetCurrentUserRole()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.IsInRole("Admin") == true) return "Admin";
        if (user?.IsInRole("Teacher") == true) return "Teacher";
        if (user?.IsInRole("Student") == true) return "Student";
        return "";
    }
}
