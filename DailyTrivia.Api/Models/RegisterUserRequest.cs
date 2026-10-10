namespace DailyTrivia.Api.Models;

public record RegisterUserRequest(
    string Email,
    string Password
);