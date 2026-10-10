using DailyTrivia.Api.Data;
using Microsoft.EntityFrameworkCore;
using DailyTrivia.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Identity.Data;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;



var builder = WebApplication.CreateBuilder(args);

//builder.Services.AddDbContext<TriviaDbContext>(
//    options => options.UseSqlite("Data Source=trivia.db")
//);

builder.Services.AddDbContext<TriviaDbContext>(
    options => options.UseNpgsql(builder.Configuration["ConnectionStrings:TriviaDb"])
);

builder.Services.AddCors(options =>{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:5174") // Vite's default dev port
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddIdentityCore<IdentityUser>(options =>
{
    options.Password.RequireNonAlphanumeric = false;
}).AddEntityFrameworkStores<TriviaDbContext>();


var JwtKey = builder.Configuration["Jwt:Key"]!;
var JwtIssuer = builder.Configuration["Jwt:Issuer"]!;
var JwtAudience = builder.Configuration["Jwt:Audience"]!;

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = JwtIssuer,
        ValidAudience = JwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey)),
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.UseCors("AllowReactApp");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TriviaDbContext>();

    if (!db.Questions.Any())
    {
        db.Questions.AddRange(
            new Question
            {
                Text = "What is the capital of France?",
                OptionA = "Berlin",
                OptionB = "Madrid",
                OptionC = "Paris",
                OptionD = "Rome",
                CorrectOption = "C"
            },
            new Question
            {
                Text = "What is the addition of 2 + 2?",
                OptionA = "2",
                OptionB = "4",
                OptionC = "3",
                OptionD = "None of the above",
                CorrectOption = "B"
            },
            new Question
            {
                Text = "Which of these birds is extinct?",
                OptionA = "Sparrow",
                OptionB = "Peacock",
                OptionC = "Ostrich",
                OptionD = "Dodo",
                CorrectOption = "D"
            },
            new Question
            {
                Text = "What is the largest ocean on Earth?",
                OptionA = "Pacific",
                OptionB = "Indian",
                OptionC = "Arctic",
                OptionD = "Atlantic",
                CorrectOption = "A"
            }
        );

        db.SaveChanges();
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/api/question-of-the-day", async (TriviaDbContext db) =>
{
    var questions = await db.Questions
        .OrderBy(q => q.Id)
        .ToListAsync();

    if (!questions.Any())
    {
        return Results.NotFound("No Questions Available.");
    }

    var startDate = new DateOnly(2026, 1, 1);
    var today = DateOnly.FromDateTime(DateTime.UtcNow);

    var daysSinceStart = today.DayNumber - startDate.DayNumber;

    var index = ((daysSinceStart % questions.Count) + questions.Count)
                % questions.Count;

    var q = questions[index];

    return Results.Ok(new
    {
        q.Id,
        q.Text,
        q.OptionA,
        q.OptionB,
        q.OptionC,
        q.OptionD
    });
});

app.MapPost("/api/submit-answer", async (SubmitAnswerRequest request, TriviaDbContext db, ClaimsPrincipal user) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId is null)
        return Results.Unauthorized();

    var todayDate = DateOnly.FromDateTime(DateTime.UtcNow);

    var alreadyPlayed = await db.Scores.AnyAsync(s => s.UserId == userId && s.DatePlayed == todayDate);

    if (alreadyPlayed)
    {
        return Results.BadRequest("You have already answered today's question!");
    }

    var question = await db.Questions.FindAsync(request.QuestionId);
    if (question is null)
        return Results.NotFound("Question not found.");

    var isCorrect = string.Equals(question.CorrectOption, request.SelectedOption, StringComparison.OrdinalIgnoreCase);

    var score = new Score
    {
        UserId = userId,
        QuestionId = request.QuestionId,
        IsCorrect = isCorrect,
        DatePlayed = todayDate
    };

    db.Scores.Add(score);
    await db.SaveChangesAsync();

    return Results.Ok(new { isCorrect, correctOption = question.CorrectOption });
})
.RequireAuthorization();

app.MapGet("/api/leaderboard", async (TriviaDbContext db) =>
{
    var today = DateOnly.FromDateTime(DateTime.UtcNow);

    var leaderboard = await db.Scores
        .Where(s => s.DatePlayed == today && s.IsCorrect)
        .Join(db.Users, s => s.UserId, u => u.Id, (s, u) => new { s, u })
        .GroupBy(x => x.u.Email)
        .Select(g => new { Email = g.Key, CorrectAnswers = g.Count() })
        .OrderByDescending(g => g.CorrectAnswers)
        .Take(10)
        .ToListAsync();

    return Results.Ok(leaderboard);
});

app.MapPost("/api/register", async (RegisterUserRequest request, UserManager<IdentityUser> UserManager) =>
{
    var user = new IdentityUser { UserName = request.Email, Email = request.Email };
    var results = await UserManager.CreateAsync(user, request.Password);

    if (!results.Succeeded)
    {
        return Results.BadRequest(results.Errors.Select(e => e.Description));
    }
    return Results.Ok("User Registration Successful");
});

app.MapPost("/api/login", async (LoginUserRequest request, UserManager<IdentityUser> userManager) =>
{
    var user = await userManager.FindByEmailAsync(request.Email);
    if(user == null)
    {
        return Results.Unauthorized();
    }
    var passwordValid = await userManager.CheckPasswordAsync(user, request.Password);
    if (!passwordValid) {
        return Results.Unauthorized();
    }

    var claims = new[]{
        new Claim(ClaimTypes.NameIdentifier, user.Id),
        new Claim(ClaimTypes.Email, user.Email)
    };

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var token = new JwtSecurityToken(
        issuer: JwtIssuer,
        audience: JwtAudience,
        claims: claims,
        expires: DateTime.UtcNow.AddDays(7),
        signingCredentials: creds
    );

    return Results.Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token) });

});

app.MapGet("/api/my-status", async (TriviaDbContext db, ClaimsPrincipal user) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId is null)
        return Results.Unauthorized();

    var today = DateOnly.FromDateTime(DateTime.UtcNow);

    var existingScore = await db.Scores
        .Where(s => s.UserId == userId && s.DatePlayed == today)
        .Select(s => new { s.IsCorrect, s.QuestionId })
        .FirstOrDefaultAsync();

    if (existingScore is null)
        return Results.Ok(new { hasPlayed = false });

    var question = await db.Questions.FindAsync(existingScore.QuestionId);

    return Results.Ok(new
    {
        hasPlayed = true,
        isCorrect = existingScore.IsCorrect,
        correctOption = question?.CorrectOption
    });
})
.RequireAuthorization();


app.Run();