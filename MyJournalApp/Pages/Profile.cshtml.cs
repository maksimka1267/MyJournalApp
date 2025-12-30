using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

[Authorize]
[IgnoreAntiforgeryToken]
public class ProfileModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ProfileModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [BindProperty] public string Email { get; set; } = string.Empty;
    [BindProperty] public string Role { get; set; } = string.Empty;
    [BindProperty] public string NewPassword { get; set; } = string.Empty;
    public string? StatusMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        // берём jwt из куки
        if (!Request.Cookies.TryGetValue("cookies", out var jwt) || string.IsNullOrWhiteSpace(jwt))
            return RedirectToPage("/Account/Login");

        var client = _httpClientFactory.CreateClient(); // без BaseAddress
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var meResp = await client.GetAsync(ApiUrl("/api/Auth/me"));
        if (!meResp.IsSuccessStatusCode)
        {
            ErrorMessage = "Не вдалося отримати дані профілю.";
            return Page();
        }

        var json = await meResp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Email = root.GetProperty("email").GetString() ?? string.Empty;
        Role = root.GetProperty("role").GetString() ?? string.Empty;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!Request.Cookies.TryGetValue("cookies", out var jwt) || string.IsNullOrWhiteSpace(jwt))
            return RedirectToPage("/Account/Login");

        if (string.IsNullOrWhiteSpace(NewPassword))
        {
            ErrorMessage = "Пароль не може бути порожнім.";
            return await OnGetAsync();
        }

        var client = _httpClientFactory.CreateClient(); // без BaseAddress
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var dto = new { NewPassword };
        using var content = new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json");

        var resp = await client.PutAsync(ApiUrl("/api/Auth/change-password"), content);
        if (!resp.IsSuccessStatusCode)
        {
            ErrorMessage = "Не вдалося змінити пароль.";
            return await OnGetAsync();
        }

        StatusMessage = "✅ Пароль оновлено!";
        NewPassword = string.Empty;
        return await OnGetAsync();
    }

    // строим абсолютный URL к API на этом же домене
    private string ApiUrl(string relativePath)
    {
        var path = relativePath.StartsWith("/") ? relativePath : "/" + relativePath;
        return $"{Request.Scheme}://{Request.Host}{path}";
    }
}
