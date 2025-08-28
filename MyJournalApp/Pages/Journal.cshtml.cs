using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyJournalApp.Data.Models;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json; // ⬅️ для GetFromJsonAsync / PostAsJsonAsync / ReadFromJsonAsync
using System.Text.Json;

namespace MyJournalApp.Pages;

public class JournalColumn
{
    public DateTime Date { get; set; }
    public string Topic { get; set; } = string.Empty;
}

public class JournalModel : PageModel
{
    private readonly HttpClient _httpClient;

    public string Role { get; set; } = "";
    public Guid UserId { get; set; }

    public List<JournalEntry> Journals { get; set; } = new();
    public List<Grade> Grades { get; set; } = new();
    public List<Student> Students { get; set; } = new();
    public List<Teacher> Teachers { get; set; } = new();
    public List<User> Users { get; set; } = new();

    public Dictionary<Guid, string> StudentNames { get; set; } = new();
    public Dictionary<Guid, string> TeacherNames { get; set; } = new();
    public List<JournalColumn> JournalColumns { get; set; } = new();

    [BindProperty] public CreateJournalModel NewJournal { get; set; } = new();
    [BindProperty] public UpdateDayGradesModel GradesForUpdate { get; set; } = new();
    [BindProperty] public Guid SelectedJournalId { get; set; }
    [BindProperty] public string SelectedTeacher { get; set; }
    public HashSet<Guid> CuratedGroupIds { get; set; } = new();
    public bool IsCuratorOnlyForSelectedJournal =>
        Role == "Teacher"
        && SelectedJournal != null
        && CuratedGroupIds.Contains(SelectedJournal.GroupId)
        && !(SelectedJournal.TeacherId?.Contains(UserId) ?? false);

    public Dictionary<Guid, string> GroupNames { get; set; } = new();
    [TempData] public string? FlashMessage { get; set; }

    public JournalModel(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient(); // ⬅️ без BaseAddress
    }

    public async Task<IActionResult> OnGetAsync(Guid? selectedJournalId = null)
    {
        var token = Request.Cookies["cookies"];
        if (string.IsNullOrEmpty(token)) return RedirectToPage("/Account/Login");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var meResp = await _httpClient.GetAsync(ApiUrl("/api/Auth/me"));
        if (!meResp.IsSuccessStatusCode) return RedirectToPage("/Account/Login");
        var meJson = await meResp.Content.ReadAsStringAsync();
        var me = JsonDocument.Parse(meJson).RootElement;
        Role = me.GetProperty("role").GetString()!;
        UserId = Guid.Parse(me.GetProperty("id").GetString()!);

        // 1) Базовые данные
        switch (Role)
        {
            case "Student": await LoadStudentView(); break;
            case "Teacher": await LoadTeacherBaseData(); break;
            case "Admin": await LoadAdminBaseData(); break;
        }

        // 2) Выбор журнала
        if (selectedJournalId.HasValue && Journals.Any(j => j.Id == selectedJournalId.Value))
            SelectedJournalId = selectedJournalId.Value;
        else if (Journals.Any())
            SelectedJournalId = Journals.OrderByDescending(j => j.Date).First().Id;

        // 3) Детали выбранного журнала
        if (SelectedJournalId != Guid.Empty)
        {
            var selectedJournal = Journals.First(j => j.Id == SelectedJournalId);
            await LoadStudentsAndGradesForJournal(selectedJournal.GroupId);
            BuildJournalColumns();
        }
        return Page();
    }

    private void BuildJournalColumns()
    {
        var existingColumns = Grades
            .GroupBy(g => g.Created.Date)
            .Select(group => new JournalColumn
            {
                Date = group.Key,
                Topic = group.FirstOrDefault()?.Comment ?? "Оцінки"
            })
            .ToDictionary(c => c.Date, c => c.Topic);

        var today = DateTime.Today;
        var endDate = today.AddMonths(1);

        for (var date = today; date <= endDate; date = date.AddDays(1))
        {
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                continue;

            if (!existingColumns.ContainsKey(date))
                existingColumns[date] = "";
        }

        JournalColumns = existingColumns
            .Select(kvp => new JournalColumn { Date = kvp.Key, Topic = kvp.Value })
            .OrderBy(c => c.Date)
            .ToList();
    }

    public async Task<IActionResult> OnPostAsync(string? handler)
    {
        var token = Request.Cookies["cookies"];
        if (string.IsNullOrEmpty(token))
            return RedirectToPage("/Account/Login");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var meResp = await _httpClient.GetAsync(ApiUrl("/api/Auth/me"));
        if (!meResp.IsSuccessStatusCode)
            return RedirectToPage("/Account/Login");

        var meJson = await meResp.Content.ReadAsStringAsync();
        var me = JsonDocument.Parse(meJson).RootElement;
        Role = me.GetProperty("role").GetString()!;
        UserId = Guid.Parse(me.GetProperty("id").GetString()!);

        return handler switch
        {
            "CreateJournal" => await OnPostCreateJournalAsync(),
            "DeleteJournal" => await OnPostDeleteJournalAsync(Guid.Parse(Request.Form["id"])),
            "DeleteAllJournals" => await OnPostDeleteAllJournalsAsync(),
            "UpdateGrades" => await OnPostUpdateGradesAsync(),
            "AddSpecialGrades" => await OnPostAddSpecialGradesAsync(),
            _ => await OnGetAsync(SelectedJournalId)
        };
    }

    public async Task<IActionResult> OnPostAddSpecialGradesAsync()
    {
        if (await IsCuratorOnlyForJournalAsync(GradesForUpdate.JournalId))
        {
            FlashMessage = "У вас лише перегляд журналу кураторської групи.";
            return RedirectToPage(new { selectedJournalId = GradesForUpdate.JournalId });
        }

        if (string.IsNullOrWhiteSpace(GradesForUpdate.Comment) || GradesForUpdate.Date == default)
        {
            FlashMessage = "Помилка: Не обрано тему або дату для спеціальної колонки.";
            return RedirectToPage(new { selectedJournalId = GradesForUpdate.JournalId });
        }

        var token = Request.Cookies["cookies"];
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        bool hasError = false;
        foreach (var (studentKey, gradeValue) in GradesForUpdate.Grades)
        {
            if (!gradeValue.HasValue) continue;
            GradesForUpdate.Presence.TryGetValue(studentKey, out bool? presenceValue);

            var newGrade = new Grade
            {
                StudentId = Guid.TryParse(studentKey, out var parsedId) ? parsedId : Guid.Empty,
                JournalEntryId = GradesForUpdate.JournalId,
                Value = gradeValue.Value,
                Comment = GradesForUpdate.Comment,
                TeacherId = UserId,
                Created = GradesForUpdate.Date,
                IsPresent = presenceValue,
            };
            var response = await _httpClient.PostAsJsonAsync(ApiUrl("/api/Grade"), newGrade);
            if (!response.IsSuccessStatusCode) hasError = true;
        }

        FlashMessage = hasError ? "Виникли помилки при збереженні." : "Тематичні оцінки успішно додано.";
        return RedirectToPage(new { selectedJournalId = GradesForUpdate.JournalId });
    }

    private async Task LoadStudentView()
    {
        var grades = await _httpClient.GetFromJsonAsync<List<Grade>>(ApiUrl($"/api/Grade/byStudent/{UserId}"));
        if (grades == null || !grades.Any()) return;

        Grades = grades;
        var journalIds = Grades.Select(g => g.JournalEntryId).Distinct();
        var teacherIds = grades.Select(g => g.TeacherId).Distinct().ToList();

        if (teacherIds.Any())
        {
            var distinctIds = teacherIds.Distinct().ToList();
            var query = string.Join("&", distinctIds.Select(id => $"ids={Uri.EscapeDataString(id.ToString())}"));
            var url = ApiUrl($"/api/User/teacher?{query}");

            var usersResponse = await _httpClient.GetAsync(url);
            if (usersResponse.IsSuccessStatusCode)
            {
                var teacherUsers = await usersResponse.Content.ReadFromJsonAsync<List<User>>();
                if (teacherUsers != null) Users.AddRange(teacherUsers);
            }
        }

        foreach (var journalId in journalIds)
        {
            SelectedJournalId = journalId;
            var journalResp = await _httpClient.GetAsync(ApiUrl($"/api/Journal/{journalId}"));
            if (journalResp.IsSuccessStatusCode)
            {
                var journal = await journalResp.Content.ReadFromJsonAsync<JournalEntry>();
                if (journal != null) Journals.Add(journal);
            }
        }
    }

    private async Task LoadTeacherBaseData()
    {
        var myJournals = await _httpClient.GetFromJsonAsync<List<JournalEntry>>(ApiUrl("/api/Journal/my")) ?? new();
        Journals.AddRange(myJournals);

        var curatedGroups = await _httpClient.GetFromJsonAsync<List<Group>>(ApiUrl("/api/Group/curated-by/me")) ?? new();
        CuratedGroupIds = curatedGroups.Select(g => g.Id).ToHashSet();

        if (curatedGroups.Any())
        {
            var allJournals = await _httpClient.GetFromJsonAsync<List<JournalEntry>>(ApiUrl("/api/Journal/all")) ?? new();
            var curatedIds = curatedGroups.Select(g => g.Id);
            Journals.AddRange(allJournals.Where(j => curatedIds.Contains(j.GroupId)));
        }
        Journals = Journals.DistinctBy(j => j.Id).ToList();

        Users = await _httpClient.GetFromJsonAsync<List<User>>(ApiUrl("/api/User/users")) ?? new();

        var groupIds = Journals.Select(j => j.GroupId).Distinct();
        var allGroups = await _httpClient.GetFromJsonAsync<List<Group>>(ApiUrl("/api/Group/all")) ?? new();

        foreach (var id in groupIds)
            GroupNames[id] = allGroups.FirstOrDefault(g => g.Id == id)?.Name ?? "Невідомо";
    }

    private async Task<bool> IsCuratorOnlyForJournalAsync(Guid journalId)
    {
        if (Role != "Teacher") return false;

        var journal = await _httpClient.GetFromJsonAsync<JournalEntry>(ApiUrl($"/api/Journal/{journalId}"));
        if (journal == null) return false;

        bool isAssignedTeacher = journal.TeacherId?.Contains(UserId) ?? false;
        if (isAssignedTeacher) return false;

        var curated = await _httpClient.GetFromJsonAsync<List<Group>>(ApiUrl("/api/Group/curated-by/me")) ?? new();
        return curated.Any(g => g.Id == journal.GroupId);
    }

    private async Task LoadAdminBaseData()
    {
        Journals = await _httpClient.GetFromJsonAsync<List<JournalEntry>>(ApiUrl("/api/Journal/all")) ?? new();
        var groups = await _httpClient.GetFromJsonAsync<List<Group>>(ApiUrl("/api/Group/all")) ?? new();
        Teachers = await _httpClient.GetFromJsonAsync<List<Teacher>>(ApiUrl("/api/User/teachers")) ?? new();
        Users = await _httpClient.GetFromJsonAsync<List<User>>(ApiUrl("/api/User/users")) ?? new();

        foreach (var group in groups) GroupNames[group.Id] = group.Name;

        foreach (var teacher in Teachers)
        {
            var user = Users.FirstOrDefault(u => u.Id == teacher.Id);
            if (user != null) TeacherNames[teacher.Id] = user.FullName;
        }
        Teachers = Teachers
            .OrderBy(t => TeacherNames.GetValueOrDefault(t.Id, "\uFFFF"))
            .ToList();
    }

    private async Task LoadStudentsAndGradesForJournal(Guid groupId)
    {
        Students.Clear();

        var groupUsers = await _httpClient.GetFromJsonAsync<List<User>>(ApiUrl($"/api/User/by-group/{groupId}"));
        if (groupUsers != null)
        {
            foreach (var user in groupUsers)
            {
                Students.Add(new Student { Id = user.Id, GroupId = groupId });
                StudentNames[user.Id] = user.FullName;
                if (!Users.Any(u => u.Id == user.Id))
                    Users.Add(user);
            }
        }

        Grades.Clear();
        var gradesForSelectedJournal =
            await _httpClient.GetFromJsonAsync<List<Grade>>(ApiUrl($"/api/Grade/journal/{SelectedJournalId}"));
        if (gradesForSelectedJournal != null)
            Grades.AddRange(gradesForSelectedJournal);
    }

    public async Task<IActionResult> OnPostCreateJournalAsync()
    {
        TempData["Debug"] = "Метод OnPostCreateJournalAsync сработал!";

        if (NewJournal.GroupId == Guid.Empty || NewJournal.TeacherId == Guid.Empty || string.IsNullOrWhiteSpace(NewJournal.Subject))
        {
            ModelState.AddModelError("", "Усі поля мають бути заповнені.");
            return Page();
        }

        var token = Request.Cookies["cookies"];
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var newJournal = new JournalEntry
        {
            Id = Guid.NewGuid(),
            Name = NewJournal.Name,
            Subject = NewJournal.Subject!,
            MaxValue = NewJournal.MaxValue,
            Date = DateTime.UtcNow,
            GroupId = NewJournal.GroupId,
            TeacherId = new List<Guid> { NewJournal.TeacherId },
            Comment = ""
        };

        var resp = await _httpClient.PostAsJsonAsync(ApiUrl("/api/Journal"), newJournal);

        if (resp.IsSuccessStatusCode)
        {
            FlashMessage = "Журнал успішно створено";
            return RedirectToPage();
        }

        ModelState.AddModelError("", "Не вдалося створити журнал.");
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateGradesAsync()
    {
        var token = Request.Cookies["cookies"];
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (await IsCuratorOnlyForJournalAsync(GradesForUpdate.JournalId))
        {
            FlashMessage = "У вас лише перегляд журналу кураторської групи.";
            return RedirectToPage(new { selectedJournalId = GradesForUpdate.JournalId });
        }

        bool hasError = false;

        var gradeKeys = GradesForUpdate.Grades?.Keys ?? Enumerable.Empty<string>();
        var presenceKeys = GradesForUpdate.Presence?.Keys ?? Enumerable.Empty<string>();
        var topicDateKeys = GradesForUpdate.Topics?.Keys ?? Enumerable.Empty<string>();

        var allCompositeKeys = new HashSet<string>(gradeKeys, StringComparer.OrdinalIgnoreCase);
        foreach (var k in presenceKeys) allCompositeKeys.Add(k);

        var dateKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var k in allCompositeKeys)
        {
            if (TrySplitKey(k, out _, out var dk) &&
                DateTime.TryParseExact(dk, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                dateKeys.Add(dk);
            }
        }
        foreach (var dk in topicDateKeys)
        {
            if (!string.IsNullOrWhiteSpace(dk) &&
                DateTime.TryParseExact(dk, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                dateKeys.Add(dk);
            }
        }

        if (dateKeys.Count == 0)
        {
            FlashMessage = "Немає змін для збереження.";
            return RedirectToPage(new { selectedJournalId = GradesForUpdate.JournalId });
        }

        var existingByDate = new Dictionary<string, List<Grade>>(StringComparer.Ordinal);
        foreach (var dk in dateKeys)
        {
            var d = DateTime.ParseExact(dk, "yyyyMMdd", CultureInfo.InvariantCulture).Date;
            var resp = await _httpClient.GetAsync(ApiUrl($"/api/Grade/journal/{GradesForUpdate.JournalId}/date/{d:yyyy-MM-dd}"));
            var list = resp.IsSuccessStatusCode
                ? await resp.Content.ReadFromJsonAsync<List<Grade>>() ?? new()
                : new List<Grade>();
            existingByDate[dk] = list;
        }

        foreach (var compositeKey in allCompositeKeys)
        {
            if (!TrySplitKey(compositeKey, out var studentId, out var dateKey)) continue;
            if (!DateTime.TryParseExact(dateKey, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) continue;
            date = date.Date;

            GradesForUpdate.Grades.TryGetValue(compositeKey, out int? val);
            GradesForUpdate.Presence.TryGetValue(compositeKey, out bool? presenceValue);

            string? topicForDate = null;
            GradesForUpdate.Topics?.TryGetValue(dateKey, out topicForDate);
            if (string.IsNullOrWhiteSpace(topicForDate))
                topicForDate = string.IsNullOrWhiteSpace(GradesForUpdate.Comment) ? null : GradesForUpdate.Comment;

            var listForDate = existingByDate[dateKey];
            var existing = listForDate.FirstOrDefault(g =>
                g.StudentId == studentId &&
                g.JournalEntryId == GradesForUpdate.JournalId &&
                g.Created.Date == date);

            if (existing != null)
            {
                var shouldDelete = !val.HasValue && !presenceValue.HasValue;
                if (shouldDelete)
                {
                    var delResp = await _httpClient.DeleteAsync(ApiUrl($"/api/Grade/{existing.Id}"));
                    if (!delResp.IsSuccessStatusCode) hasError = true;
                    listForDate.Remove(existing);
                    continue;
                }

                if (val.HasValue) existing.Value = val.Value;
                if (presenceValue.HasValue) existing.IsPresent = presenceValue;
                if (!string.IsNullOrWhiteSpace(topicForDate)) existing.Comment = topicForDate;

                var updateResp = await _httpClient.PutAsJsonAsync(ApiUrl($"/api/Grade/{existing.Id}"), existing);
                if (!updateResp.IsSuccessStatusCode) hasError = true;
            }
            else if (val.HasValue || presenceValue.HasValue)
            {
                var newGrade = new Grade
                {
                    Id = Guid.NewGuid(),
                    StudentId = studentId,
                    JournalEntryId = GradesForUpdate.JournalId,
                    Value = val ?? 0,
                    Comment = topicForDate ?? "",
                    TeacherId = UserId,
                    Created = date,
                    IsPresent = presenceValue
                };
                var createResp = await _httpClient.PostAsJsonAsync(ApiUrl("/api/Grade"), newGrade);
                if (!createResp.IsSuccessStatusCode) hasError = true;
                listForDate.Add(newGrade);
            }
        }

        if (GradesForUpdate.Topics != null)
        {
            foreach (var (dk, topic) in GradesForUpdate.Topics)
            {
                if (string.IsNullOrWhiteSpace(topic)) continue;
                if (!existingByDate.TryGetValue(dk, out var list)) continue;

                foreach (var g in list)
                {
                    if (g.Comment == topic) continue;
                    g.Comment = topic;
                    var upd = await _httpClient.PutAsJsonAsync(ApiUrl($"/api/Grade/{g.Id}"), g);
                    if (!upd.IsSuccessStatusCode) hasError = true;
                }
            }
        }

        FlashMessage = hasError ? "Під час збереження виникли помилки." : "Зміни успішно збережено.";
        return RedirectToPage(new { selectedJournalId = GradesForUpdate.JournalId });
    }

    private static bool TrySplitKey(string key, out Guid studentId, out string dateKey)
    {
        studentId = Guid.Empty;
        dateKey = "";
        if (string.IsNullOrEmpty(key)) return false;

        int lastDash = key.LastIndexOf('-');
        if (lastDash <= 0 || lastDash >= key.Length - 1) return false;

        var guidPart = key.Substring(0, lastDash);
        dateKey = key.Substring(lastDash + 1);
        return Guid.TryParse(guidPart, out studentId);
    }

    public async Task<IActionResult> OnPostDeleteJournalAsync(Guid id)
    {
        var token = Request.Cookies["cookies"];
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // ⚠️ здесь оставил POST, как у тебя — если на сервере именно такой маршрут.
        var journalResp = await _httpClient.PostAsync(ApiUrl($"/api/Journal/{id}"), content: null);
        if (journalResp.StatusCode != HttpStatusCode.NoContent)
        {
            ModelState.AddModelError("", "Не вдалося видалити журнал.");
            return await OnGetAsync();
        }

        var gradesResp = await _httpClient.GetAsync(ApiUrl($"/api/Grade/journal/{id}"));
        if (gradesResp.IsSuccessStatusCode)
        {
            var grades = await gradesResp.Content.ReadFromJsonAsync<List<Grade>>();
            foreach (var grade in grades ?? new())
                await _httpClient.DeleteAsync(ApiUrl($"/api/Grade/{grade.Id}"));
        }

        FlashMessage = "Журнал та оцінки успішно видалено.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAllJournalsAsync()
    {
        var token = Request.Cookies["cookies"];
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var journalResp = await _httpClient.GetAsync(ApiUrl("/api/Journal/all"));
        if (!journalResp.IsSuccessStatusCode)
        {
            ModelState.AddModelError("", "Не вдалося отримати список журналів.");
            return await OnGetAsync();
        }

        var journals = await journalResp.Content.ReadFromJsonAsync<List<JournalEntry>>();
        foreach (var journal in journals ?? new())
        {
            var gradesResp = await _httpClient.GetAsync(ApiUrl($"/api/Grade/journal/{journal.Id}"));
            if (gradesResp.IsSuccessStatusCode)
            {
                var grades = await gradesResp.Content.ReadFromJsonAsync<List<Grade>>();
                foreach (var grade in grades ?? new())
                    await _httpClient.DeleteAsync(ApiUrl($"/api/Grade/{grade.Id}"));
            }

            await _httpClient.DeleteAsync(ApiUrl($"/api/Journal/{journal.Id}"));
        }

        FlashMessage = "Усі журнали та оцінки успішно видалено.";
        return RedirectToPage();
    }

    public JournalEntry? SelectedJournal =>
        Journals.FirstOrDefault(j => j.Id == SelectedJournalId);

    public List<Grade> SelectedJournalGrades =>
        Grades.Where(g => g.JournalEntryId == SelectedJournalId).ToList();

    public List<string> TopicHeaders =>
        SelectedJournalGrades
            .Select(g => g.Comment)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .ToList();

    // Абсолютный URL к своему API на этом же хосте
    private string ApiUrl(string relativePath)
    {
        var path = relativePath.StartsWith("/") ? relativePath : "/" + relativePath;
        return $"{Request.Scheme}://{Request.Host}{path}";
    }
}

public class CreateJournalModel
{
    public string Name { get; set; }
    public string? Subject { get; set; }
    public int MaxValue {  get; set; }
    public Guid GroupId { get; set; }
    public Guid TeacherId { get; set; }
}

public class UpdateDayGradesModel
{
    public Guid JournalId { get; set; }
    public DateTime Date { get; set; }
    public string? Comment { get; set; }

    public Dictionary<string, int?> Grades { get; set; } = new(); // "studentGuid-yyyymmdd" -> оценка
    public Dictionary<string, string?> Topics { get; set; } = new(); // "yyyymmdd" -> тема
    public Dictionary<string, bool?> Presence { get; set; } = new();
}
