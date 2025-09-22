using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyJournalApp.Data.Models;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MyJournalApp.Pages;

public class JournalColumn
{
    public DateTime Date { get; set; }
    public string Topic { get; set; } = string.Empty;

    // Ключ (стабильный) для различения колонок с одинаковой датой
    public string TopicKey => MakeTopicKey(Topic);

    public static string MakeTopicKey(string? topic)
        => string.IsNullOrWhiteSpace(topic)
            ? "no-topic"
            : new string(
                topic.Trim().ToLowerInvariant()
                     .Where(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')
                     .ToArray()
            );
}

public class JournalModel : PageModel
{
    private readonly HttpClient _httpClient;

    public string Role { get; set; } = "";
    public Guid UserId { get; set; }
    public class ExportGradesRequest
    {
        public Guid StudentId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
    [BindProperty] public ExportGradesRequest ExportGrades { get; set; } = new();
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
    [BindProperty] public string SelectedTeacher { get; set; } = "";
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
        _httpClient = httpClientFactory.CreateClient();
    }

    public async Task<IActionResult> OnGetAsync(Guid? selectedJournalId = null)
    {
        var token = Request.Cookies["cookies"];
        if (string.IsNullOrEmpty(token)) return RedirectToPage("/Account/Login");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // me
        var meResp = await _httpClient.GetAsync(ApiUrl("/api/Auth/me"));
        if (!meResp.IsSuccessStatusCode) return RedirectToPage("/Account/Login");
        var meJson = await meResp.Content.ReadAsStringAsync();
        var me = JsonDocument.Parse(meJson).RootElement;
        Role = me.GetProperty("role").GetString()!;
        UserId = Guid.Parse(me.GetProperty("id").GetString()!);

        // базовые данные
        switch (Role)
        {
            case "Student": await LoadStudentView(); break;
            case "Teacher": await LoadTeacherBaseData(); break;
            case "Admin": await LoadAdminBaseData(); break;
        }

        // выбрать журнал
        if (selectedJournalId.HasValue && Journals.Any(j => j.Id == selectedJournalId.Value))
            SelectedJournalId = selectedJournalId.Value;
        else if (Journals.Any())
            SelectedJournalId = Journals.OrderByDescending(j => j.Date).First().Id;

        // детали выбранного журнала
        // детали выбранного журнала
        if (SelectedJournalId != Guid.Empty)
        {
            var j = Journals.First(x => x.Id == SelectedJournalId);

            // ВАЖНО: для студента не перезатираем его полный список оценок
            if (Role != "Student")
            {
                await LoadStudentsAndGradesForJournal(j.GroupId);
                BuildJournalColumns();
            }
        }


        return Page();
    }
    private void BuildJournalColumns()
    {
        JournalColumns = Grades
            .GroupBy(g => new {
                Date = g.Created.Date,
                TopicKey = JournalColumn.MakeTopicKey(g.Comment)
            })
            .Select(gr => new {
                Date = gr.Key.Date,
                Topic = gr.Select(x => x.Comment)
                          .FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)) ?? "",
                FirstCreated = gr.Min(x => x.Created) // опорный час колонки
            })
            .OrderBy(x => x.Date)
            .ThenBy(x => x.FirstCreated)            // РАНЕЕ созданная колонка — левее
                                                    //.ThenBy(x => x.Topic)                 // опционально: тай-брейкер при равных временах
            .Select(x => new JournalColumn { Date = x.Date, Topic = x.Topic })
            .ToList();
    }
    public async Task<IActionResult> OnPostAsync(string? handler)
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

        return handler switch
        {
            "CreateJournal" => await OnPostCreateJournalAsync(),
            "DeleteJournal" => await OnPostDeleteJournalAsync(Guid.Parse(Request.Form["id"])),
            "DeleteAllJournals" => await OnPostDeleteAllJournalsAsync(),
            "UpdateGrades" => await OnPostUpdateGradesAsync(),
            "AddSpecialGrades" => await OnPostAddSpecialGradesAsync(),
            "GenerateJournals" => await OnPostGenerateJournalsAsync(),
            "CreateColumn" => await OnPostCreateColumnAsync(),
            "DeleteColumn" => await OnPostDeleteColumnAsync(),
            "ExportStudentGrades" => await OnPostExportStudentGradesAsync(),
            "DownloadIndividualPlan" => await OnPostDownloadIndividualPlanAsync(),
            _ => await OnGetAsync(SelectedJournalId)
        };
    }
    private async Task<IActionResult> OnPostDownloadIndividualPlanAsync()
    {
        ModelState.Remove(nameof(SelectedTeacher));
        var token = Request.Cookies["cookies"];
        if (string.IsNullOrEmpty(token)) return RedirectToPage("/Account/Login");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // дергаем наш API
        var url = ApiUrl("/api/IndividualPlan/me");
        var resp = await _httpClient.GetAsync(url);
        if (!resp.IsSuccessStatusCode)
        {
            FlashMessage = "Не вдалося сформувати індивідуальний план.";
            return await OnGetAsync(SelectedJournalId);
        }

        var bytes = await resp.Content.ReadAsByteArrayAsync();
        var cd = resp.Content.Headers.ContentDisposition;
        var fileName = cd?.FileNameStar ?? cd?.FileName ?? "Індивідуальний_план.xlsx";

        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    public async Task<IActionResult> OnPostExportStudentGradesAsync()
    {
        // базовая валидация
        if (ExportGrades.StudentId == Guid.Empty || ExportGrades.StartDate == default || ExportGrades.EndDate == default)
        {
            FlashMessage = "Вкажіть студента та коректний діапазон дат.";
            return RedirectToPage(new { selectedJournalId = SelectedJournalId });
        }

        if (ExportGrades.EndDate < ExportGrades.StartDate)
        {
            FlashMessage = "Дата кінця менша за початок.";
            return RedirectToPage(new { selectedJournalId = SelectedJournalId });
        }

        var token = Request.Cookies["cookies"];
        if (string.IsNullOrEmpty(token)) return RedirectToPage("/Account/Login");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var url = ApiUrl($"/api/StudentGradesReport/student-grades/export?studentId={ExportGrades.StudentId}&start={ExportGrades.StartDate:yyyy-MM-dd}&end={ExportGrades.EndDate:yyyy-MM-dd}");
        var resp = await _httpClient.GetAsync(url);
        if (!resp.IsSuccessStatusCode)
        {
            FlashMessage = "Не вдалося сформувати рапортичку оцінок.";
            return RedirectToPage(new { selectedJournalId = SelectedJournalId });
        }

        var bytes = await resp.Content.ReadAsByteArrayAsync();
        var fileName = GetFileNameFromContentDisposition(resp)
                       ?? $"Рапортичка_оцінок_{ExportGrades.StartDate:yyyyMMdd}-{ExportGrades.EndDate:yyyyMMdd}.xlsx";
        var contentType = resp.Content.Headers.ContentType?.ToString()
                          ?? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        return File(bytes, contentType, fileName);
    }

    private static string? GetFileNameFromContentDisposition(HttpResponseMessage resp)
    {
        if (resp.Content.Headers.ContentDisposition?.FileNameStar != null)
            return resp.Content.Headers.ContentDisposition.FileNameStar.Trim('"');
        if (resp.Content.Headers.ContentDisposition?.FileName != null)
            return resp.Content.Headers.ContentDisposition.FileName.Trim('"');
        return null;
    }
    public async Task<IActionResult> OnPostDeleteColumnAsync()
    {
        // авторизация
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Request.Cookies["cookies"]);

        // журнал
        Guid journalId = SelectedJournalId;
        if (journalId == Guid.Empty && Guid.TryParse(Request.Form["JournalId"], out var jid))
            journalId = jid;

        if (journalId == Guid.Empty)
        {
            FlashMessage = "Журнал не обрано.";
            return RedirectToPage();
        }

        // запрет для «лише куратора»
        if (Role == "Teacher" && await IsCuratorOnlyForJournalAsync(journalId))
        {
            FlashMessage = "У вас лише перегляд журналу кураторської групи.";
            return RedirectToPage(new { selectedJournalId = journalId });
        }

        // параметры колонки
        var dateKey = (Request.Form["DateKey"].ToString() ?? "").Trim();
        var topicKey = (Request.Form["TopicKey"].ToString() ?? "").Trim();

        if (string.IsNullOrWhiteSpace(dateKey) || string.IsNullOrWhiteSpace(topicKey))
        {
            FlashMessage = "Невірні параметри колонки.";
            return RedirectToPage(new { selectedJournalId = journalId });
        }

        if (!DateTime.TryParseExact(dateKey, "yyyyMMdd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            FlashMessage = "Невірний формат дати.";
            return RedirectToPage(new { selectedJournalId = journalId });
        }

        // достаем все оценки за эту дату, дальше фильтруем по topicKey
        var resp = await _httpClient.GetAsync(ApiUrl($"/api/Grade/journal/{journalId}/date/{date:yyyy-MM-dd}"));
        if (!resp.IsSuccessStatusCode)
        {
            FlashMessage = "Не вдалося отримати оцінки для цієї дати.";
            return RedirectToPage(new { selectedJournalId = journalId });
        }

        var grades = await resp.Content.ReadFromJsonAsync<List<Grade>>() ?? new List<Grade>();
        var toDelete = grades
            .Where(g => JournalColumn.MakeTopicKey(g.Comment) == topicKey)
            .ToList();

        if (toDelete.Count == 0)
        {
            FlashMessage = "Оцінок для цієї колонки не знайдено (можливо, вона вже видалена).";
            return RedirectToPage(new { selectedJournalId = journalId });
        }

        bool hasError = false;
        foreach (var g in toDelete)
        {
            var del = await _httpClient.DeleteAsync(ApiUrl($"/api/Grade/{g.Id}"));
            if (!del.IsSuccessStatusCode) hasError = true;
        }

        FlashMessage = hasError
            ? "Колонку видалено частково (деякі записи не вдалося видалити)."
            : "Колонку видалено.";

        return RedirectToPage(new { selectedJournalId = journalId });
    }

    public async Task<IActionResult> OnPostCreateColumnAsync()
    {
        if (SelectedJournalId == Guid.Empty &&
            Guid.TryParse(Request.Form["SelectedJournalId"], out var fromForm))
        {
            SelectedJournalId = fromForm;
        }
        if (SelectedJournalId == Guid.Empty)
        {
            FlashMessage = "Журнал не обрано.";
            return RedirectToPage();
        }

        // запрет для «лише куратора»
        if (Role == "Teacher" && await IsCuratorOnlyForJournalAsync(SelectedJournalId))
        {
            FlashMessage = "У вас лише перегляд журналу кураторської групи.";
            return RedirectToPage(new { selectedJournalId = SelectedJournalId });
        }

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Request.Cookies["cookies"]);

        var dateStr = Request.Form["NewColumn.Date"];
        var topic = (Request.Form["NewColumn.Topic"].ToString() ?? "").Trim();

        if (!DateTime.TryParse(dateStr, out var date) || date == default)
        {
            FlashMessage = "Невірна дата.";
            return RedirectToPage(new { selectedJournalId = SelectedJournalId });
        }

        // журнал и студенты
        var journalResp = await _httpClient.GetAsync(ApiUrl($"/api/Journal/{SelectedJournalId}"));
        if (!journalResp.IsSuccessStatusCode)
        {
            FlashMessage = "Журнал не знайдено.";
            return RedirectToPage(new { selectedJournalId = SelectedJournalId });
        }
        var journal = await journalResp.Content.ReadFromJsonAsync<JournalEntry>();

        var groupUsers = await _httpClient.GetFromJsonAsync<List<User>>(ApiUrl($"/api/User/by-group/{journal!.GroupId}")) ?? new();
        var studentIds = groupUsers.Select(u => u.Id).ToList();

        // существующие на эту дату
        var existingResp = await _httpClient.GetAsync(ApiUrl($"/api/Grade/journal/{SelectedJournalId}/date/{date:yyyy-MM-dd}"));
        var existingGrades = existingResp.IsSuccessStatusCode
            ? await existingResp.Content.ReadFromJsonAsync<List<Grade>>() ?? new()
            : new List<Grade>();

        // --- вычисляем итоговую тему с автонумерацией ---
        var sameDayTopics = existingGrades
            .Where(g => g.Created.Date == date.Date)
            .Select(g => g.Comment ?? "")
            .ToList();

        var finalTopic = topic;
        if (sameDayTopics.Contains(topic))
        {
            int counter = 2;
            while (sameDayTopics.Contains($"{topic} #{counter}"))
                counter++;
            finalTopic = $"{topic} #{counter}";
        }

        bool hasError = false;
        foreach (var sid in studentIds)
        {
            var newGrade = new Grade
            {
                Id = Guid.NewGuid(),
                StudentId = sid,
                JournalEntryId = SelectedJournalId,
                Value = 0,                    // «неатестований»
                Comment = finalTopic,
                TeacherId = UserId,
                Created = date.Date,
                IsPresent = null
            };

            var resp = await _httpClient.PostAsJsonAsync(ApiUrl("/api/Grade"), newGrade);
            if (!resp.IsSuccessStatusCode) hasError = true;
        }

        FlashMessage = hasError
            ? "Колонку створено частково (деякі записи не збереглися)."
            : "Колонку створено.";
        return RedirectToPage(new { selectedJournalId = SelectedJournalId });
    }


    public async Task<IActionResult> OnPostGenerateJournalsAsync()
    {
        if (Role != "Admin") return Forbid();

        var response = await _httpClient.PostAsync(ApiUrl("/api/Journal/generate-from-schedule"), null);
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<GenerationResult>();
            FlashMessage = result?.Message ?? "Операція генерації журналів виконана.";
        }
        else
        {
            FlashMessage = "Помилка під час генерації журналів.";
        }
        return RedirectToPage();
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

        var date = GradesForUpdate.Date.Date;
        bool hasError = false;

        var existingResp = await _httpClient.GetAsync(ApiUrl($"/api/Grade/journal/{GradesForUpdate.JournalId}/date/{date:yyyy-MM-dd}"));
        var existingGrades = existingResp.IsSuccessStatusCode
            ? await existingResp.Content.ReadFromJsonAsync<List<Grade>>() ?? new()
            : new List<Grade>();

        var existingByStudent = existingGrades
            .GroupBy(g => g.StudentId)
            .ToDictionary(gr => gr.Key, gr => gr.First());
        foreach (var (studentKey, gradeValue) in GradesForUpdate.Grades)
        {
            if (!Guid.TryParse(studentKey, out var studentId)) continue;
            if (!gradeValue.HasValue) continue;

            GradesForUpdate.Presence.TryGetValue(studentKey, out bool? presenceValue);

            if (existingByStudent.TryGetValue(studentId, out var existing))
            {
                existing.Value = gradeValue.Value;
                existing.IsPresent = presenceValue;
                existing.Comment = GradesForUpdate.Comment;
                existing.TeacherId = UserId;

                var upd = await _httpClient.PutAsJsonAsync(ApiUrl($"/api/Grade/{existing.Id}"), existing);
                if (!upd.IsSuccessStatusCode) hasError = true;
            }
            else
            {
                var newGrade = new Grade
                {
                    Id = Guid.NewGuid(),
                    StudentId = studentId,
                    JournalEntryId = GradesForUpdate.JournalId,
                    Value = gradeValue.Value,
                    Comment = GradesForUpdate.Comment,
                    TeacherId = UserId,
                    Created = date,
                    IsPresent = presenceValue
                };
                var resp = await _httpClient.PostAsJsonAsync(ApiUrl("/api/Grade"), newGrade);
                if (!resp.IsSuccessStatusCode) hasError = true;
            }
        }

        // Синхронизируем тему только в той колонке, которую добавляем
        foreach (var g in existingGrades
            .Where(g => g.Comment == GradesForUpdate.Comment))
        {
            g.Comment = GradesForUpdate.Comment; // можно опустить, если не меняли
            var upd = await _httpClient.PutAsJsonAsync(ApiUrl($"/api/Grade/{g.Id}"), g);
            if (!upd.IsSuccessStatusCode) hasError = true;
        }
        FlashMessage = hasError ? "Виникли помилки при збереженні." : "Колонку успішно збережено/оновлено.";
        return RedirectToPage(new { selectedJournalId = GradesForUpdate.JournalId });
    }

    /// <summary>
    /// ОБНОВЛЕНИЕ ОЦЕНОК (новая схема ключей: studentGuid-yyyymmdd-topicKey)
    /// </summary>
    public async Task<IActionResult> OnPostUpdateGradesAsync()
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Request.Cookies["cookies"]);

        if (await IsCuratorOnlyForJournalAsync(GradesForUpdate.JournalId))
        {
            FlashMessage = "У вас лише перегляд журналу кураторської групи.";
            return RedirectToPage(new { selectedJournalId = GradesForUpdate.JournalId });
        }

        bool hasError = false;

        var gradeKeys = GradesForUpdate?.Grades?.Keys ?? Enumerable.Empty<string>();
        var presenceKeys = GradesForUpdate?.Presence?.Keys ?? Enumerable.Empty<string>();

        var allCompositeKeys = new HashSet<string>(gradeKeys, StringComparer.OrdinalIgnoreCase);
        foreach (var k in presenceKeys) allCompositeKeys.Add(k);

        // Собираем все (dateKey, topicKey), чтобы заранее вытянуть записи по датам
        var dateTopicKeys = new HashSet<(string dateKey, string topicKey)>(new DateTopicComparer());
        foreach (var k in allCompositeKeys)
        {
            if (TrySplitKey3(k, out _, out var dk, out var tk))
                dateTopicKeys.Add((dk, tk));
        }

        if (dateTopicKeys.Count == 0)
        {
            FlashMessage = "Немає змін для збереження.";
            return RedirectToPage(new { selectedJournalId = GradesForUpdate.JournalId });
        }

        // Загружаем по каждой дате, группируем по topicKey
        var existingByDateTopic = new Dictionary<(string dateKey, string topicKey), List<Grade>>(new DateTopicComparer());
        foreach (var (dateKey, topicKey) in dateTopicKeys)
        {
            var d = DateTime.ParseExact(dateKey, "yyyyMMdd", CultureInfo.InvariantCulture).Date;
            var resp = await _httpClient.GetAsync(ApiUrl($"/api/Grade/journal/{GradesForUpdate.JournalId}/date/{d:yyyy-MM-dd}"));
            var list = resp.IsSuccessStatusCode
                ? await resp.Content.ReadFromJsonAsync<List<Grade>>() ?? new()
                : new List<Grade>();

            foreach (var grp in list.GroupBy(g => JournalColumn.MakeTopicKey(g.Comment)))
                existingByDateTopic[(dateKey, grp.Key)] = grp.ToList();
        }

        // Обработка ячеек
        foreach (var compositeKey in allCompositeKeys)
        {
            if (!TrySplitKey3(compositeKey, out var studentId, out var dateKey, out var topicKey)) continue;
            if (!DateTime.TryParseExact(dateKey, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) continue;
            date = date.Date;

            // --- ФИКС: инициализация переменных ---
            int? val = null;
            bool? presenceValue = null;

            GradesForUpdate.Grades?.TryGetValue(compositeKey, out val);
            GradesForUpdate.Presence?.TryGetValue(compositeKey, out presenceValue);

            // --- ФИКС: topicOverride ---
            string? topicOverride = null;
            if (GradesForUpdate?.Topics != null &&
                GradesForUpdate.Topics.TryGetValue(dateKey, out var tmpTopic) &&
                !string.IsNullOrWhiteSpace(tmpTopic))
            {
                topicOverride = tmpTopic.Trim();
            }

            string topicForCell =
                topicOverride ??
                (existingByDateTopic.TryGetValue((dateKey, topicKey), out var bucket) && bucket.Any()
                    ? (bucket.First().Comment ?? "")
                    : "");

            if (!existingByDateTopic.TryGetValue((dateKey, topicKey), out var listForBucket))
            {
                listForBucket = new List<Grade>();
                existingByDateTopic[(dateKey, topicKey)] = listForBucket;
            }

            var existing = listForBucket.FirstOrDefault(g =>
                g.StudentId == studentId &&
                g.JournalEntryId == GradesForUpdate.JournalId &&
                g.Created.Date == date);

            if (existing != null)
            {
                bool shouldDelete = !val.HasValue && !presenceValue.HasValue;
                if (shouldDelete)
                {
                    var delResp = await _httpClient.DeleteAsync(ApiUrl($"/api/Grade/{existing.Id}"));
                    if (!delResp.IsSuccessStatusCode) hasError = true;
                    listForBucket.Remove(existing);
                    continue;
                }

                if (val.HasValue) existing.Value = val.Value;
                if (presenceValue.HasValue) existing.IsPresent = presenceValue;
                if (!string.IsNullOrWhiteSpace(topicForCell)) existing.Comment = topicForCell;

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
                    Comment = topicForCell,
                    TeacherId = UserId,
                    Created = date,
                    IsPresent = presenceValue
                };
                var createResp = await _httpClient.PostAsJsonAsync(ApiUrl("/api/Grade"), newGrade);
                if (!createResp.IsSuccessStatusCode) hasError = true;
                listForBucket.Add(newGrade);
            }
        }
        // Если передали Topics[dateKey] — обновим тему у всех записей бакетов той даты
        if (GradesForUpdate?.Topics != null)
        {
            foreach (var (dk, newTopic) in GradesForUpdate.Topics.Where(p => !string.IsNullOrWhiteSpace(p.Value)))
            {
                foreach (var kv in existingByDateTopic.Where(x => x.Key.dateKey == dk))
                {
                    foreach (var g in kv.Value)
                    {
                        if (g.Comment == newTopic) continue;
                        g.Comment = newTopic!;
                        var upd = await _httpClient.PutAsJsonAsync(ApiUrl($"/api/Grade/{g.Id}"), g);
                        if (!upd.IsSuccessStatusCode) hasError = true;
                    }
                }
            }
        }

        FlashMessage = hasError ? "Під час збереження виникли помилки." : "Зміни успішно збережено.";
        return RedirectToPage(new { selectedJournalId = GradesForUpdate.JournalId });
    }

    private sealed class DateTopicComparer : IEqualityComparer<(string dateKey, string topicKey)>
    {
        public bool Equals((string dateKey, string topicKey) x, (string dateKey, string topicKey) y)
            => string.Equals(x.dateKey, y.dateKey, StringComparison.Ordinal)
            && string.Equals(x.topicKey, y.topicKey, StringComparison.Ordinal);

        public int GetHashCode((string dateKey, string topicKey) obj)
            => HashCode.Combine(obj.dateKey, obj.topicKey);
    }

    // Ключ формата: <guid>-<yyyyMMdd>-<topicKey>
    private static bool TrySplitKey3(string key, out Guid studentId, out string dateKey, out string topicKey)
    {
        studentId = Guid.Empty;
        dateKey = "";
        topicKey = "";

        if (string.IsNullOrWhiteSpace(key)) return false;

        // Формат: <guid>-<yyyyMMdd>-<topicKey>
        // GUID = 36 символов с дефисами, дата = ровно 8 цифр, topicKey — всё остальное (может содержать дефисы/нижние подчёркивания)
        var m = Regex.Match(key, @"^(?<guid>[0-9a-fA-F-]{36})-(?<date>\d{8})-(?<topic>.+)$");
        if (!m.Success) return false;

        if (!Guid.TryParse(m.Groups["guid"].Value, out studentId)) return false;

        dateKey = m.Groups["date"].Value;
        // на всякий случай проверим дату
        if (dateKey.Length != 8) return false;

        topicKey = m.Groups["topic"].Value; // тут можем оставить как есть — это уже нормализованный MakeTopicKey
        return true;
    }

    public async Task<IActionResult> OnPostDeleteJournalAsync(Guid id)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Request.Cookies["cookies"]);

        var journalResp = await _httpClient.DeleteAsync(ApiUrl($"/api/Journal/{id}"));
        if (journalResp.StatusCode != HttpStatusCode.NoContent)
        {
            ModelState.AddModelError("", "Не вдалося видалити журнал.");
            return await OnGetAsync();
        }
        FlashMessage = "Журнал та оцінки успішно видалено.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAllJournalsAsync()
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Request.Cookies["cookies"]);

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

    private async Task LoadStudentView()
    {
        // 1) Пытаемся взять все оценки студента «как есть»
        var grades = await _httpClient.GetFromJsonAsync<List<Grade>>(ApiUrl($"/api/Grade/byStudent/{UserId}"));

        // 2) Fallback: если роут пустой/недоступен — собираем из журналов
        if (grades == null || grades.Count == 0)
        {
            var allJournals = await _httpClient.GetFromJsonAsync<List<JournalEntry>>(ApiUrl("/api/Journal/all")) ?? new();
            var acc = new List<Grade>();

            foreach (var j in allJournals)
            {
                // берём все оценки журнала и фильтруем по студенту
                var gj = await _httpClient.GetFromJsonAsync<List<Grade>>(ApiUrl($"/api/Grade/journal/{j.Id}")) ?? new();
                if (gj.Count > 0)
                    acc.AddRange(gj.Where(g => g.StudentId == UserId));
            }

            grades = acc;
        }

        Grades = grades ?? new();

        // 3) Подтягиваем журналы по найденным оценкам
        Journals.Clear();
        var journalIds = Grades.Select(g => g.JournalEntryId).Distinct().ToList();
        foreach (var journalId in journalIds)
        {
            var jr = await _httpClient.GetAsync(ApiUrl($"/api/Journal/{journalId}"));
            if (jr.IsSuccessStatusCode)
            {
                var journal = await jr.Content.ReadFromJsonAsync<JournalEntry>();
                if (journal != null) Journals.Add(journal);
            }
        }

        // если ничего не нашли — просто выходим (UI покажет «Оберіть журнал…»)
        if (Journals.Count == 0) return;

        // 4) Подгружаем teacher users для тултипов
        var teacherIds = Grades.Select(g => g.TeacherId).Distinct().ToList();
        if (teacherIds.Count > 0)
        {
            var query = string.Join("&", teacherIds.Select(id => $"ids={Uri.EscapeDataString(id.ToString())}"));
            var url = ApiUrl($"/api/User/teacher?{query}");
            var usersResponse = await _httpClient.GetAsync(url);
            if (usersResponse.IsSuccessStatusCode)
            {
                var teacherUsers = await usersResponse.Content.ReadFromJsonAsync<List<User>>();
                if (teacherUsers != null)
                    Users.AddRange(teacherUsers.Where(u => Users.All(x => x.Id != u.Id)));
            }
        }

        // 5) Выбор журнала по умолчанию — самый свежий по дате журнала
        SelectedJournalId = Journals.OrderByDescending(j => j.Date).First().Id;
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
        Teachers = Teachers.OrderBy(t => TeacherNames.GetValueOrDefault(t.Id, "\uFFFF")).ToList();
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

    private async Task LoadStudentsAndGradesForJournal(Guid groupId)
    {
        if(Role == "Student")
        {
            return;
        }
        Students.Clear();

        var groupUsers = await _httpClient.GetFromJsonAsync<List<User>>(ApiUrl($"/api/User/by-group/{groupId}"));
        if (groupUsers != null)
        {
            foreach (var user in groupUsers)
            {
                Students.Add(new Student { Id = user.Id, GroupId = groupId });
                StudentNames[user.Id] = user.FullName;
                if (!Users.Any(u => u.Id == user.Id)) Users.Add(user);
            }
        }

        Grades.Clear();
        var gradesForSelectedJournal =
            await _httpClient.GetFromJsonAsync<List<Grade>>(ApiUrl($"/api/Grade/journal/{SelectedJournalId}"));
        if (gradesForSelectedJournal != null) Grades.AddRange(gradesForSelectedJournal);
    }

    public async Task<IActionResult> OnPostCreateJournalAsync()
    {
        // минимальная валидация — поля из модалки создания
        if (NewJournal.GroupId == Guid.Empty || NewJournal.TeacherId == Guid.Empty)
        {
            ModelState.AddModelError("", "Усі поля мають бути заповнені.");
            return Page();
        }

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Request.Cookies["cookies"]);

        var newJournal = new JournalEntry
        {
            Id = Guid.NewGuid(),
            Name = NewJournal.Name,
            MaxValue = NewJournal.MaxValue,
            Date = DateTime.UtcNow,
            Subject = "створено вручну",
            GroupId = NewJournal.GroupId,
            TeacherId = new List<Guid> { NewJournal.TeacherId },
            Comment = ""
        };

        var resp = await _httpClient.PostAsJsonAsync(ApiUrl("/api/Journal"), newJournal);
        var body = await resp.Content.ReadAsStringAsync();
        Console.WriteLine($"POST /api/Journal -> {(int)resp.StatusCode} {resp.StatusCode}\n{body}");
        if (resp.IsSuccessStatusCode)
        {
            FlashMessage = "Журнал успішно створено";
            return RedirectToPage();
        }

        ModelState.AddModelError("", "Не вдалося створити журнал.");
        return Page();
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

    private string ApiUrl(string relativePath)
    {
        var path = relativePath.StartsWith("/") ? relativePath : "/" + relativePath;
        return $"{Request.Scheme}://{Request.Host}{path}";
    }
}

public class CreateJournalModel
{
    public string Name { get; set; } = "";
    public string? Subject { get; set; }          // оставлено для совместимости формы
    public int MaxValue { get; set; }
    public Guid GroupId { get; set; }
    public Guid TeacherId { get; set; }
}

public class GenerationResult
{
    public bool Success { get; set; }
    public int CreatedCount { get; set; }
    public string Message { get; set; } = "";
}

public class UpdateDayGradesModel
{
    public Guid JournalId { get; set; }
    public DateTime Date { get; set; }            // используется в AddSpecialGrades
    public string? Comment { get; set; }          // используется в AddSpecialGrades

    // Ключ ЯЧЕЙКИ: "studentGuid-yyyymmdd-topicKey"
    public Dictionary<string, int?> Grades { get; set; } = new();
    public Dictionary<string, bool?> Presence { get; set; } = new();

    // Опционально: переименовать тему у ВСЕХ колонок выбранной даты.
    // Ключ: "yyyymmdd"
    public Dictionary<string, string?> Topics { get; set; } = new();
}
