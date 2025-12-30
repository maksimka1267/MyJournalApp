using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http;
using System.Text;
using System.Text.Json;

[IgnoreAntiforgeryToken]
public class LoginModel : PageModel
{
    [BindProperty] public string Email { get; set; } = string.Empty;
    [BindProperty] public string Password { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }

    private readonly IHttpClientFactory _httpClientFactory;
    public LoginModel(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Введіть email та пароль.";
            return Page();
        }

        try
        {
            // Абсолютные URL к своему API (тот же хост и схема, где крутится страница)
            var baseUri = $"{Request.Scheme}://{Request.Host}";
            var loginUrl = $"{baseUri}/api/Auth/login";
            var meUrl = $"{baseUri}/api/Auth/me";

            // Клиент без авто-редиректов и без авто-cookie — куки прокинем вручную в ответ браузеру
            var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false };
            using var client = _httpClientFactory.CreateClient(); // без BaseAddress
            using var raw = new HttpClient(handler);

            var dto = new { Email, Password };
            using var content = new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json");

            // 1) Логинимся
            using var loginResp = await raw.PostAsync(loginUrl, content);

            if (loginResp.IsSuccessStatusCode)
            {
                // 2) Прокидываем все Set-Cookie в ответ браузеру
                string? authCookiePair = null; // "cookies=...."
                if (loginResp.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders))
                {
                    foreach (var cookie in setCookieHeaders)
                    {
                        Response.Headers.Append("Set-Cookie", cookie);

                        var firstPart = cookie.Split(';', 2)[0].Trim();
                        if (firstPart.StartsWith("cookies=", StringComparison.OrdinalIgnoreCase))
                            authCookiePair ??= firstPart;
                    }
                }

                // 3) Проверяем /api/Auth/me и решаем, куда редиректить
                if (!string.IsNullOrEmpty(authCookiePair))
                {
                    using var meReq = new HttpRequestMessage(HttpMethod.Get, meUrl);
                    meReq.Headers.Add("Cookie", authCookiePair);

                    using var meResp = await raw.SendAsync(meReq);
                    if (meResp.IsSuccessStatusCode)
                    {
                        var json = await meResp.Content.ReadAsStringAsync();
                        var me = JsonSerializer.Deserialize<MeDto>(json, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (me is not null && me.MustChangePassword)
                            return Redirect("/Profile");
                    }
                }

                return Redirect("/");
            }

            ErrorMessage = loginResp.StatusCode == System.Net.HttpStatusCode.Unauthorized
                ? "Невірний логін або пароль."
                : $"Помилка входу: {(int)loginResp.StatusCode}";
            return Page();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Внутрішня помилка: {ex.Message}";
            return Page();
        }
    }

    private sealed class MeDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = "";
        public string Role { get; set; } = "";
        public bool MustChangePassword { get; set; }
    }
}
