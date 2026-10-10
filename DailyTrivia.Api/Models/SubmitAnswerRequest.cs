namespace DailyTrivia.Api.Models;

public record SubmitAnswerRequest(
    string PlayerName,
    int QuestionId,
    string SelectedOption
);