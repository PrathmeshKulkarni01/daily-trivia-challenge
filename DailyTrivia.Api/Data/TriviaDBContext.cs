using DailyTrivia.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DailyTrivia.Api.Data;

public class TriviaDbContext : IdentityDbContext<IdentityUser>
{
    public TriviaDbContext(DbContextOptions<TriviaDbContext> options) : base(options) {}
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Score> Scores => Set<Score>();
} 