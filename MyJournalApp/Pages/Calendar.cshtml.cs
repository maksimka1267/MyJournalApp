using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using static AcademicProcessController;

public class CalendarModel : PageModel
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CalendarModel(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor)
    {
        _httpClient = httpClientFactory.CreateClient(); // без BaseAddress
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

        // Группы и выбор текущей группы
        AllGroups = await _httpClient.GetFromJsonAsync<List<Group>>(ApiUrl("/api/Group/all")) ?? new();

        if (Role == "Student")
            SelectedGroupId = AllGroups.FirstOrDefault(g => g.StudentIds.Contains(userId))?.Id ?? Guid.Empty;
        else
            SelectedGroupId = groupId ?? AllGroups.FirstOrDefault()?.Id ?? Guid.Empty;

        if (SelectedGroupId == Guid.Empty)
            return Page();

        // === Учебный период: 1 сентября (академ. стартовый год) -> 30 июня (следующий год)
        var now = DateTime.Now;
        var academicStartYear = (now.Month >= 7) ? now.Year : now.Year-1;

        var sept1 = new DateTime(academicStartYear, 9, 1);
        var juneEnd = new DateTime(academicStartYear + 1, 6, DateTime.DaysInMonth(academicStartYear + 1, 6));

        var periodStart = NextMondayOrSame(sept1);
        var periodEnd = EndOfWeekSunday(juneEnd);

        // Тянем события по двум годам
        var evY1 = await _httpClient.GetFromJsonAsync<List<AcademicEvent>>(
            ApiUrl($"/api/AcademicProcess/{SelectedGroupId}/{academicStartYear}")) ?? new();

        var evY2 = await _httpClient.GetFromJsonAsync<List<AcademicEvent>>(
            ApiUrl($"/api/AcademicProcess/{SelectedGroupId}/{academicStartYear + 1}")) ?? new();

        var existingEvents = evY1.Concat(evY2)
            .Where(e => e.StartDate.Date >= sept1.Date && e.EndDate.Date <= periodEnd.Date)
            .ToList();

        // Строим сетку недель Пн–Вс
        var fullYearEvents = new List<AcademicEvent>();
        int weekIndex = 1;
        for (var weekStart = periodStart; weekStart <= periodEnd; weekStart = weekStart.AddDays(7), weekIndex++)
        {
            var weekEnd = EndOfWeekSunday(weekStart);

            var existing = existingEvents.FirstOrDefault(e =>
                e.StartDate.Date == weekStart.Date && e.EndDate.Date == weekEnd.Date);

            if (existing != null)
            {
                existing.WeekNumber = weekIndex;
                existing.Year = weekStart.Year;
                existing.Month = weekStart.Month;
                fullYearEvents.Add(existing);
            }
            else
            {
                fullYearEvents.Add(new AcademicEvent
                {
                    Id = Guid.NewGuid(),
                    GroupId = SelectedGroupId,
                    Year = weekStart.Year,
                    Month = weekStart.Month,
                    WeekNumber = weekIndex,
                    Type = AcademicWeekType.Lecture,
                    StartDate = weekStart,
                    EndDate = weekEnd
                });
            }
        }

        DisplayEvents = fullYearEvents;

        // Режим редактирования — переносим в DTO
        if (Request.Query["edit"] == "true")
        {
            EditableEvents = DisplayEvents.Select(ev => new AcademicEventDto
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

            ModelState.Clear();
        }

        return Page();

        // ====== Локальные функции (без отката в август) ======
        static DateTime NextMondayOrSame(DateTime date)
        {
            int diff = ((int)DayOfWeek.Monday - (int)date.DayOfWeek + 7) % 7;
            return date.Date.AddDays(diff);
        }

        static DateTime EndOfWeekSunday(DateTime date)
        {
            int shiftToSunday = ((int)DayOfWeek.Sunday - (int)date.DayOfWeek + 7) % 7;
            return date.Date.AddDays(shiftToSunday);
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var token = Request.Cookies["cookies"];
        if (string.IsNullOrEmpty(token)) return RedirectToPage("/Account/Login");

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var putUrl = ApiUrl("/api/AcademicProcess/bulk");
        var response = await _httpClient.PutAsJsonAsync(putUrl, EditableEvents);

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

    // Собираем абсолютный URL к своему же API (тот же хост/схема, что и страница)
    private string ApiUrl(string relativePath)
    {
        // нормализуем: всегда с одним ведущим слешем
        var path = relativePath.StartsWith("/") ? relativePath : "/" + relativePath;
        return $"{Request.Scheme}://{Request.Host}{path}";
    }
}
