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

    [BindProperty] public string Email { get; set; }
    [BindProperty] public string Role { get; set; }
    [BindProperty] public string NewPassword { get; set; }
    public string StatusMessage { get; set; }
    public string ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var client = _httpClientFactory.CreateClient("ApiClient");
        var request = new HttpRequestMessage(HttpMethod.Get, "api/Auth/me"); // ✅ без /

        // Передай cookie JWT токен
        if (Request.Cookies.TryGetValue("cookies", out var jwt))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = "Не вдалося отримати дані.";
            return Page();
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Email = root.GetProperty("email").GetString();
        Role = root.GetProperty("role").GetString();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(NewPassword))
        {
            ErrorMessage = "Пароль не може бути порожнім.";
            return await OnGetAsync();
        }

        var dto = new { NewPassword };
        var json = JsonSerializer.Serialize(dto);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var client = _httpClientFactory.CreateClient("ApiClient");
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/Auth/change-password")
        {
            Content = content
        };

        if (Request.Cookies.TryGetValue("cookies", out var jwt))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = "Не вдалося змінити пароль.";
            return await OnGetAsync();
        }

        StatusMessage = "✅ Пароль оновлено!";
        return await OnGetAsync();
    }
}