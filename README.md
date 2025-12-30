# MyJournalApp — Електронний журнал для коледжу

**MyJournalApp** — веб-застосунок “електронний журнал” для ведення навчального процесу в коледжі: групи, користувачі, розклад занять, журнал оцінок/відвідуваності, звіти та експорт у Excel.

---

## Основні можливості

* **Ролі та доступи:** адміністратор / викладач / студент
* **Групи та користувачі:** створення / перегляд / керування
* **Розклад:** заняття, типи тижнів, події/академічні активності
* **Журнал:** оцінки, відвідування, тематичні/підсумкові колонки (залежно від налаштувань)
* **Експорт у Excel (ClosedXML):** журнали/звіти
* **API + UI:** проєкт поєднує ASP.NET Core Web API (Controllers) і Razor Pages для інтерфейсу

---

## Технології

* **.NET 8 (ASP.NET Core)**
* **Razor Pages (UI)**
* **Web API Controllers**
* **Entity Framework Core + SQL Server**
* **JWT Bearer (токен) + збереження токена в cookie**
* **Swagger (Swashbuckle)**
* **ClosedXML (експорт у Excel)**

---

## Структура проєкту (високорівнево)

* `Pages/` — Razor Pages (UI): головна, журнал, групи, користувачі, профіль, календар тощо
* `Controller/` — API контролери (Auth, Schedule, Journal, Grades, Reports…)
* `Data/` — DbContext + моделі (Student, Teacher, Group, Lesson, Grade, JournalEntry…)
* `Repository/` — доступ до даних (патерн Repository)
* `Service/` — бізнес-логіка (генерація звітів/експорт, логіка журналу тощо)
* `Migrations/` — міграції EF Core
* `wwwroot/` — статичні ресурси

---

## Як запустити локально

### Вимоги

* **.NET SDK 8.x**
* **SQL Server** (або **LocalDB** на Windows)

### Кроки

1. **Клонувати репозиторій:**

```bash
git clone <repo_url>
```

2. **Відкрити рішення** `MyJournalApp.sln` у Visual Studio / Rider.

3. **Перевірити рядок підключення** в `MyJournalApp/appsettings.json`:

* `ConnectionStrings:DefaultConnection`

4. **Відновити залежності:**

```bash
dotnet restore
```

5. **Застосувати міграції** (якщо використовуєш EF Core CLI):

```bash
dotnet tool restore
dotnet ef database update --project MyJournalApp
```

6. **Запустити:**

```bash
dotnet run --project MyJournalApp
```

### Після запуску

* **UI:** `https://localhost:<port>/`
* **Swagger:** `https://localhost:<port>/swagger`

---

## Авторизація

* Вхід виконується через `POST /api/auth/login`.
* Після успішного входу сервер генерує **JWT** і кладе його в **cookie (HttpOnly)**.
* Для API-запитів токен зчитується з cookie автоматично (налаштовано в `Program.cs`).

---

## Примітки

* У проєкті є діагностичний endpoint `/_db-ping` (для перевірки підключення до БД). Використовуй лише в **dev-середовищі**.
