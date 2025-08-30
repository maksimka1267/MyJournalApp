using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MyJournalApp.Auth;
using MyJournalApp.Data;
using MyJournalApp.Data.Models;
using MyJournalApp.Interface;
using MyJournalApp.Jwt;
using MyJournalApp.Repository;
using MyJournalApp.Service;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// MVC + Razor Pages
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// HTTP Client for API calls in Razor Pages
builder.Services.AddHttpClient("ApiClient", c =>
{
    c.BaseAddress = new Uri("/api/", UriKind.Relative); // обратим внимание на завершающий слэш
});



// HttpContextAccessor для Razor Pages
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<JsonOptions>(options =>
{
    options.JsonSerializerOptions.Converters.Add(new DateOnlyJsonConverter());
});

// Database (устойчивое подключение)
builder.Services.AddDbContext<JournalDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), null)
    ));

// JWT
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddScoped<IJwtProvider, JwtProvider>();

// Password Hasher
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();

// Authentication
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.TokenValidationParameters = new()
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SecretKey"]))
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                context.Token = context.Request.Cookies["cookies"];
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// Repositories
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IStudentRepository, StudentRepository>();
builder.Services.AddScoped<ITeacherRepository, TeacherRepository>();
builder.Services.AddScoped<IGradeRepository, GradeRepository>();
builder.Services.AddScoped<IAdminRepository, AdminRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ILessonRepository, LessonRepository>();
builder.Services.AddScoped<IGroupRepository, GroupRepository>();
builder.Services.AddScoped<IJournalEntryRepository, JournalEntryRepository>();
builder.Services.AddScoped<IScheduleRepository, ScheduleRepository>();
builder.Services.AddScoped<IAcademicEventRepository, AcademicEventRepository>();
builder.Services.AddScoped<IJournalGenerationService, JournalGenerationService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Swagger всегда доступен (не только в Dev)
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "MyJournal API v1");
    options.RoutePrefix = "swagger";
});

// Static files, routing, auth
app.UseStaticFiles();
app.UseRouting();

app.UseCookiePolicy();
app.UseAuthentication();
app.UseAuthorization();

// Корневой редирект
app.MapGet("/", async context =>
{
    if (!context.User.Identity?.IsAuthenticated ?? true)
        context.Response.Redirect("/Account/Login");
    else
        context.Response.Redirect("/Index");

    await Task.CompletedTask;
});

// Диагностический эндпоинт (временно!)
app.MapGet("/_db-ping", async (IConfiguration cfg) =>
{
    var cs = cfg.GetConnectionString("DefaultConnection");
    try
    {
        await using var c = new Microsoft.Data.SqlClient.SqlConnection(cs);
        await c.OpenAsync();

        await using var cmd = new Microsoft.Data.SqlClient.SqlCommand(
            "SELECT DB_NAME() as Db, SUSER_SNAME() as LoginName", c);
        await using var r = await cmd.ExecuteReaderAsync();

        var result = new List<object>();
        while (await r.ReadAsync())
            result.Add(new { Db = r["Db"], LoginName = r["LoginName"] });

        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.ToString());
    }
});

app.MapRazorPages();
app.MapControllers();

app.Run();
