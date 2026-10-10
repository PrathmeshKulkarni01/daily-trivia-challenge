# Daily Trivia Challenge — Project Recap

A full-stack daily trivia game built to revise React + .NET skills. Backend: ASP.NET Core Web API + EF Core + Postgres (Neon). Frontend: React (Vite) with JWT-based auth.

---

## Tech Stack

- **Backend:** ASP.NET Core Web API (.NET 9), Entity Framework Core, ASP.NET Core Identity, JWT auth
- **Database:** Neon (hosted Postgres, free tier) — migrated from local SQLite
- **Frontend:** React (Vite), Context API for auth state
- **Styling:** Custom CSS (Fraunces + Manrope fonts, dark indigo/gold theme)

---

## Project Structure

```
daily-trivia-challenge/
├── DailyTrivia.Api/          # .NET backend
│   ├── Models/
│   │   ├── Question.cs
│   │   ├── Score.cs
│   │   └── (RegisterUserRequest, LoginRequest — as records)
│   ├── Data/
│   │   └── TriviaDbContext.cs   # extends IdentityDbContext<IdentityUser>
│   ├── Migrations/
│   ├── Program.cs            # all endpoints + startup config
│   └── appsettings.json      # ConnectionStrings, Jwt config
└── daily-trivia-web/          # React frontend
    └── src/
        ├── App.jsx            # Quiz component + app shell
        ├── AuthForm.jsx        # Login/Register form
        ├── AuthContext.jsx     # shares token/email across app
        └── index.css           # all styling
```

---

## Backend: Models

**Question.cs**
```csharp
public class Question
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public string OptionA { get; set; } = string.Empty;
    public string OptionB { get; set; } = string.Empty;
    public string OptionC { get; set; } = string.Empty;
    public string OptionD { get; set; } = string.Empty;
    public string CorrectOption { get; set; } = string.Empty; // "A"-"D"
}
```

**Score.cs**
```csharp
public class Score
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty; // ties to IdentityUser.Id
    public int QuestionId { get; set; }
    public bool IsCorrect { get; set; }
    public DateOnly DatePlayed { get; set; }
}
```

**TriviaDbContext.cs**
```csharp
public class TriviaDbContext : IdentityDbContext<IdentityUser>
{
    public TriviaDbContext(DbContextOptions<TriviaDbContext> options) : base(options) { }
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Score> Scores => Set<Score>();
}
```

---

## Backend: Key Endpoints (all in Program.cs)

| Endpoint | Method | Auth required? | Purpose |
|---|---|---|---|
| `/api/register` | POST | No | Create account (email + password) |
| `/api/login` | POST | No | Returns JWT token |
| `/api/question-of-the-day` | GET | No | Deterministic daily question (date-indexed rotation through question pool) |
| `/api/submit-answer` | POST | **Yes** | Validates answer, blocks duplicate plays per day, saves Score tied to real UserId |
| `/api/my-status` | GET | **Yes** | Checks if the logged-in user already played today; returns their result if so |
| `/api/leaderboard` | GET | No | Today's top scorers, joined with user email |

**Question of the day logic** (deterministic, Wordle-style):
```csharp
var startDate = new DateOnly(2026, 1, 1);
var today = DateOnly.FromDateTime(DateTime.UtcNow);
var daysSinceStart = today.DayNumber - startDate.DayNumber;
var index = ((daysSinceStart % questions.Count) + questions.Count) % questions.Count;
```

**Duplicate-play prevention** (in submit-answer):
```csharp
var alreadyPlayed = await db.Scores.AnyAsync(s => s.UserId == userId && s.DatePlayed == today);
if (alreadyPlayed) return Results.BadRequest("You've already played today's question.");
```

---

## Backend: Auth Setup

- **ASP.NET Core Identity** (`AddIdentityCore<IdentityUser>`) handles user creation + password hashing
- **JWT Bearer auth** configured via `AddAuthentication().AddJwtBearer(...)`
- JWT settings live in `appsettings.json` under a `Jwt` section (Key/Issuer/Audience) — **dev-only secret, must be replaced with a real secret via environment variable before deploying**
- Endpoints requiring login use `.RequireAuthorization()`
- Inside protected endpoints, get the real user via:
  ```csharp
  var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
  ```

---

## Database: SQLite → Postgres Migration

Switched from local SQLite to **Neon** (free hosted Postgres) because most free hosting platforms wipe local files on redeploy — SQLite's file-based storage doesn't survive that.

Key changes:
1. Added `Npgsql.EntityFrameworkCore.PostgreSQL` package
2. Changed `UseSqlite(...)` → `UseNpgsql(builder.Configuration.GetConnectionString("TriviaDb"))`
3. Connection string lives in `appsettings.json` under `ConnectionStrings:TriviaDb`
4. **Use the direct/unpooled connection string** (`db_url`, not `db_url_pooled`) — Neon's pooled connection can break EF Core migrations
5. Deleted old SQLite migrations, regenerated fresh with `dotnet ef migrations add InitialPostgresMigration`

`appsettings.json` structure (top-level sibling sections):
```json
{
  "ConnectionStrings": { "TriviaDb": "..." },
  "Jwt": { "Key": "...", "Issuer": "...", "Audience": "..." },
  "Logging": { ... }
}
```

---

## Frontend: Key Pieces

- **AuthContext.jsx** — holds `token` and `email` in React state (memory only, not localStorage — resets on refresh by design, for now)
- **AuthForm.jsx** — single component toggling between Login/Register
- **App.jsx** — `AppContent` shows `<AuthForm />` if no token, else `<Quiz />`
- **Quiz component** — fetches question + play-status in parallel on load; disables options after answering; highlights correct answer (gold) and wrong pick (coral)

CORS on the backend must explicitly allow the frontend's origin:
```csharp
policy.WithOrigins("http://localhost:5173", "http://localhost:5174")
      .AllowAnyHeader().AllowAnyMethod();
```
(Update this to the real deployed frontend URL once live.)

---

## Styling

- Dark indigo theme (`#1B1B3A` bg, `#252552` card) with gold (`#E8B93B`) as the sole accent color for correct answers/celebration
- **Fraunces** (serif, characterful) for headlines/questions, **Manrope** for body/UI text
- Single centered card layout, mimicking a physical quiz card

### Why these choices (design rationale)

- **The app is a daily ritual/habit game (Wordle-adjacent), not a SaaS product.** So we deliberately avoided the generic "SaaS card" look (soft shadows, light background, everything-is-a-card grid) that most AI-generated UIs default to. A game should feel a bit more like an object — a physical quiz card sitting on a table — not a dashboard.
- **Gold accent is used sparingly, only for correct/celebratory moments.** This was intentional restraint — if the accent color is everywhere (buttons, links, headers), it stops meaning anything. Saving it for "you got it right" / streaks makes those moments actually feel rewarding, like a small win.
- **Fraunces + Manrope pairing, instead of a single generic sans (like Inter) everywhere.** The question text is the actual hero of each screen — it deserves a typeface with some character (Fraunces has real personality, slightly editorial) rather than blending into generic UI chrome. Manrope handles the actual interface bits (buttons, inputs) so it stays clean and legible.
- **No eyebrow labels, no all-caps section headers, no icon-heavy decoration.** These are common "AI default" tells that make an app look templated. We kept chrome minimal so the question and the act of answering stay the visual focus.
- **One centered card instead of a busy multi-panel layout.** Given the core loop is "read one question, pick one answer," there was no reason to add visual complexity — the layout mirrors the simplicity of the actual task.

Worth remembering if styling gets extended later: keep new elements (streak badges, share cards, etc.) consistent with this restraint — resist the urge to add a second accent color or extra decorative elements just because a new feature was added.

---

## Known Gotchas We Hit (worth remembering)

- Missing `app.Run();` → app builds and silently exits, no error shown
- Package added via CLI didn't always land in `.csproj` — always verify manually
- `RegisterRequest` name collides with Identity's built-in type — renamed to `RegisterUserRequest`
- `record` declarations must go **after** `app.Run()` in top-level statement files
- CORS errors = origin/port mismatch between frontend and backend CORS policy
- SSL errors locally → simplest fix is using `http://` instead of `https://` for local dev
- EF Core migrations must be created **and applied** (`add` then `update`) — easy to forget the second step
- Neon gives two connection strings — use the **unpooled** one for EF Core

---

## Status: v1 Complete ✅

- Full auth (register/login/JWT)
- Daily rotating question
- Scoring + duplicate-play prevention
- Leaderboard
- Real styling
- Postgres migration done

## Next Steps (v2 candidates)

- [ ] Deploy backend (Render/Railway) + frontend (Vercel) — free tier
- [ ] Move real JWT secret to environment variable before deploying
- [ ] Update CORS + frontend API_BASE to point to live URLs post-deploy
- [ ] Streak tracking (consecutive correct days)
- [ ] Countdown timer to next question
- [ ] Shareable result card
- [ ] All-time leaderboard (not just today)
- [ ] Categories/difficulty levels
- [ ] Mobile responsiveness pass

---

## Note on GitHub Access

Office laptop has a policy against personal GitHub accounts. Plan: transfer project folder (zip, includes `.git` history since nothing's been pushed yet) to a personal machine, and do all `git remote add` / `git push` / deployment steps from there.
