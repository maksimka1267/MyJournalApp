using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyJournalApp.Data.Models;
using System.Net.Http.Headers;
using System.Text.Json;

namespace MyJournalApp.Pages;
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

    [BindProperty] public CreateJournalModel NewJournal { get; set; } = new();
    [BindProperty] public AddTopicModel NewTopic { get; set; } = new();
    [BindProperty] public Guid SelectedJournalId { get; set; }

    public Dictionary<Guid, string> GroupNames { get; set; } = new();
    [TempData] public string? FlashMessage { get; set; }

    public JournalModel(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("ApiClient");
    }

    public async Task<IActionResult> OnGetAsync(Guid? selectedJournalId = null)
    {
        var token = Request.Cookies["cookies"];
        if (string.IsNullOrEmpty(token)) return RedirectToPage("/Account/Login");

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var meResp = await _httpClient.GetAsync("api/Auth/me");
        if (!meResp.IsSuccessStatusCode) return RedirectToPage("/Account/Login");

        var meJson = await meResp.Content.ReadAsStringAsync();
        var me = JsonDocument.Parse(meJson).RootElement;
        Role = me.GetProperty("role").GetString()!;
        UserId = Guid.Parse(me.GetProperty("userId").GetString()!);

        switch (Role)
        {
            case "Student":
                await LoadStudentView();
                break;
            case "Teacher":
                await LoadTeacherView();
                break;
            case "Admin":
                await LoadAdminView();
                break;
        }

        if (selectedJournalId.HasValue && Journals.Any(j => j.Id == selectedJournalId.Value))
            SelectedJournalId = selectedJournalId.Value;
        else
            SelectedJournalId = Journals.FirstOrDefault()?.Id ?? Guid.Empty;

        return Page();
    }
    public async Task<IActionResult> OnPostAsync()
    {
        var token = Request.Cookies["cookies"];
        if (string.IsNullOrEmpty(token)) return RedirectToPage("/Account/Login");

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var meResp = await _httpClient.GetAsync("api/Auth/me");
        if (!meResp.IsSuccessStatusCode) return RedirectToPage("/Account/Login");

        var meJson = await meResp.Content.ReadAsStringAsync();
        var me = JsonDocument.Parse(meJson).RootElement;
        Role = me.GetProperty("role").GetString()!;
        UserId = Guid.Parse(me.GetProperty("userId").GetString()!);

        // Получаем handler из запроса
        var handler = RouteData.Values["handler"]?.ToString();

        Console.WriteLine($"Handler: {handler}");
        Console.WriteLine($"Subject: {NewJournal.Subject}");
        Console.WriteLine($"GroupId: {NewJournal.GroupId}");
        Console.WriteLine($"TeacherId: {NewJournal.TeacherId}");

        // Обработка по handler-у
        if (Role == "Admin")
        {
            switch (handler)
            {
                case "DeleteJournal":
                    var idStr = Request.Form["id"];
                    if (Guid.TryParse(idStr, out var journalId))
                        return await OnPostDeleteJournalAsync(journalId);
                    ModelState.AddModelError("", "Невірний ID журналу для видалення.");
                    return await OnGetAsync();

                case "DeleteAllJournals":
                    return await OnPostDeleteAllJournalsAsync();

                case null:
                case "CreateJournal":
                    return await OnPostCreateJournalAsync();
            }
        }

        if (Role == "Teacher" && (handler == null || handler == "AddTopic"))
        {
            return await OnPostAddTopicAsync();
        }

        ModelState.AddModelError("", "Недопустима дія для цієї ролі або невідомий запит.");
        return await OnGetAsync();
    }

    private async Task LoadStudentView()
    {
        var grades = await _httpClient.GetFromJsonAsync<List<Grade>>($"api/Grade/byStudent/{UserId}");
        if (grades == null) return;

        Grades = grades;
        var journalIds = Grades.Select(g => g.JournalEntryId).Distinct();
        foreach (var journalId in journalIds)
        {
            SelectedJournalId = journalId;
            var journalResp = await _httpClient.GetAsync($"api/Journal/{journalId}");
            if (journalResp.IsSuccessStatusCode)
            {
                var journal = await journalResp.Content.ReadFromJsonAsync<JournalEntry>();
                if (journal != null) Journals.Add(journal);
            }
        }
    }
    private async Task LoadTeacherView()
    {
        Journals = await _httpClient.GetFromJsonAsync<List<JournalEntry>>("api/Journal/my") ?? new();
        if (SelectedJournalId == Guid.Empty)
            SelectedJournalId = Journals.FirstOrDefault()?.Id ?? Guid.Empty;

        // Получаем список уникальных GroupId из журналов
        var groupIds = Journals.Select(j => j.GroupId).Distinct().ToList();

        GroupNames.Clear();
        var groups = new List<Group>();

        foreach (var groupId in groupIds)
        {
            var group = await _httpClient.GetFromJsonAsync<Group>($"api/Group/{groupId}");
            if (group != null)
            {
                GroupNames[group.Id] = group.Name;
                groups.Add(group); // сохраним, чтобы дальше грузить студентов
            }
        }
        Students.Clear();
        var studentIdSet = new HashSet<Guid>();
        foreach (var group in groups)
        {
            var groupUsers = await _httpClient.GetFromJsonAsync<List<User>>($"api/Group/{group.Id}/users");
            if (groupUsers != null)
            {
                foreach (var user in groupUsers)
                {
                    var student = new Student { Id = user.Id, GroupId = group.Id };
                    if (studentIdSet.Add(student.Id))
                        Students.Add(student);
                    StudentNames[student.Id] = user.FullName;
                    Users.Add(user);
                }
            }
        }
        Grades.Clear();
        foreach (var journal in Journals)
        {
            var grades = await _httpClient.GetFromJsonAsync<List<Grade>>($"api/Grade/journal/{journal.Id}");
            if (grades != null)
                Grades.AddRange(grades);
        }
    }

    private async Task LoadAdminView()
    {
        Journals = await _httpClient.GetFromJsonAsync<List<JournalEntry>>("api/Journal/all") ?? new();
        if (SelectedJournalId == Guid.Empty)
            SelectedJournalId = Journals.FirstOrDefault()?.Id ?? Guid.Empty;

        foreach (var journal in Journals)
        {
            var grades = await _httpClient.GetFromJsonAsync<List<Grade>>($"api/Grade/journal/{journal.Id}");
            if (grades != null) Grades.AddRange(grades);
        }

        var groups = await _httpClient.GetFromJsonAsync<List<Group>>("api/Group/all") ?? new();
        GroupNames.Clear();
        foreach (var group in groups)
            GroupNames[group.Id] = group.Name;

        Teachers = await _httpClient.GetFromJsonAsync<List<Teacher>>("api/User/teachers") ?? new();
        Students = await _httpClient.GetFromJsonAsync<List<Student>>("api/User/students") ?? new();
        Users = await _httpClient.GetFromJsonAsync<List<User>>("api/User/users") ?? new();

        foreach (var teacher in Teachers)
        {
            var user = Users.FirstOrDefault(u => u.Id == teacher.Id);
            if (user != null)
                TeacherNames[teacher.Id] = user.FullName;
        }

        foreach (var student in Students)
        {
            var user = Users.FirstOrDefault(u => u.Id == student.Id);
            if (user != null)
                StudentNames[student.Id] = user.FullName;
        }
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
            Subject = NewJournal.Subject!,
            Date = DateTime.UtcNow,
            GroupId = NewJournal.GroupId,
            TeacherId = new List<Guid> { NewJournal.TeacherId },
            Comment = ""
        };

        var content = JsonContent.Create(newJournal);
        var resp = await _httpClient.PostAsync("api/Journal", content);

        if (resp.IsSuccessStatusCode)
        {
            FlashMessage = "Журнал успішно створено";
            return RedirectToPage();
        }

        ModelState.AddModelError("", "Не вдалося створити журнал.");
        return Page();
    }

    public async Task<IActionResult> OnPostAddTopicAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTopic.Comment))
        {
            ModelState.AddModelError("", "Тема не може бути порожньою.");
            await LoadTeacherView();
            return Page();
        }

        var token = Request.Cookies["cookies"];
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var existingGrades = await _httpClient.GetFromJsonAsync<List<Grade>>($"api/Grade/journal/{SelectedJournalId}") ?? new();
        bool topicExists = existingGrades.Any(g =>
            g.Comment == NewTopic.Comment &&
            g.Created.Date == DateTime.UtcNow.Date
        );

        Console.WriteLine("📘 SelectedJournalId: " + SelectedJournalId);
        var journalResp = await _httpClient.GetAsync($"api/Journal/{SelectedJournalId}");

        if (!journalResp.IsSuccessStatusCode)
        {
            ModelState.AddModelError("", "Обраний журнал не знайдено. Можливо, він був видалений.");
            await LoadTeacherView();
            return Page();
        }

        bool hasError = false;
        foreach (var kvp in NewTopic.Grades)
        {
            var grade = new Grade
            {
                Id = Guid.NewGuid(),
                StudentId = kvp.Key,
                JournalEntryId = SelectedJournalId,
                Value = kvp.Value ?? 0,
                Comment = NewTopic.Comment,
                TeacherId = UserId
            };

            var content = JsonContent.Create(grade);
            var response = await _httpClient.PostAsync("api/Grade", content);
            if (!response.IsSuccessStatusCode)
            {
                hasError = true;
                ModelState.AddModelError("", $"Не вдалося зберегти оцінку для студента {kvp.Key}");
            }
        }

        if (hasError)
        {
            await LoadTeacherView();
            return Page();
        }

        FlashMessage = "Тему та оцінки успішно збережено.";
        return RedirectToPage();
    }
    public async Task<IActionResult> OnPostDeleteJournalAsync(Guid id)
    {
        var token = Request.Cookies["cookies"];
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Удаляем журнал
        var journalResp = await _httpClient.DeleteAsync($"api/Journal/{id}");
        if (!journalResp.IsSuccessStatusCode)
        {
            ModelState.AddModelError("", "Не вдалося видалити журнал.");
            return await OnGetAsync(); // Обновить данные
        }

        // Удаляем оценки из этого журнала
        var gradesResp = await _httpClient.GetAsync($"api/Grade/journal/{id}");
        if (gradesResp.IsSuccessStatusCode)
        {
            var grades = await gradesResp.Content.ReadFromJsonAsync<List<Grade>>();
            foreach (var grade in grades ?? new())
            {
                await _httpClient.DeleteAsync($"api/Grade/{grade.Id}");
            }
        }

        FlashMessage = "Журнал та оцінки успішно видалено.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAllJournalsAsync()
    {
        var token = Request.Cookies["cookies"];
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var journalResp = await _httpClient.GetAsync("api/Journal/all");
        if (!journalResp.IsSuccessStatusCode)
        {
            ModelState.AddModelError("", "Не вдалося отримати список журналів.");
            return await OnGetAsync();
        }

        var journals = await journalResp.Content.ReadFromJsonAsync<List<JournalEntry>>();
        foreach (var journal in journals ?? new())
        {
            // Удаление оценок
            var gradesResp = await _httpClient.GetAsync($"api/Grade/journal/{journal.Id}");
            if (gradesResp.IsSuccessStatusCode)
            {
                var grades = await gradesResp.Content.ReadFromJsonAsync<List<Grade>>();
                foreach (var grade in grades ?? new())
                {
                    await _httpClient.DeleteAsync($"api/Grade/{grade.Id}");
                }
            }

            // Удаление журнала
            await _httpClient.DeleteAsync($"api/Journal/{journal.Id}");
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
}

public class CreateJournalModel
{
    public string? Subject { get; set; }
    public Guid GroupId { get; set; }
    public Guid TeacherId { get; set; }
}

public class AddTopicModel
{
    public string? Comment { get; set; }
    public Dictionary<Guid, int?> Grades { get; set; } = new();
}
