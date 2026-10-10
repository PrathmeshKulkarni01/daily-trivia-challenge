namespace DailyTrivia.Api.Models;

public record LoginUserRequest(
    string Email,
    string Password
);