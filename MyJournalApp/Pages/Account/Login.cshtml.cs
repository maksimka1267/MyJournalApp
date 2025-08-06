using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;
using System.Text.Json;

[IgnoreAntiforgeryToken]
public class LoginModel : PageModel
{
    [BindProperty]
    public string Email { get; set; }

    [BindProperty]
    public string Password { get; set; }

    public string ErrorMessage { get; set; }

    private readonly IHttpClientFactory _httpClientFactory;

    public LoginModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        // Используем HttpClient без автоматического редиректа и cookie-менеджера
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false
        };

        var client = new HttpClient(handler);

        var dto = new
        {
            Email = Email,
            Password = Password
        };

        var json = JsonSerializer.Serialize(dto);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("https://localhost:7120/api/Auth/login", content);

        // Забираем cookie из ответа и сохраняем в браузере
        if (response.IsSuccessStatusCode &&
            response.Headers.TryGetValues("Set-Cookie", out var cookieHeaders))
        {
            foreach (var cookie in cookieHeaders)
            {
                // Устанавливаем cookie в ответ, чтобы браузер сохранил
                Response.Headers.Append("Set-Cookie", cookie);
            }

            Console.WriteLine("✅ Редіректимо на Index");
            return Redirect("/");
        }

        ErrorMessage = "Невірний логін або пароль.";
        return Page();
    }
}
