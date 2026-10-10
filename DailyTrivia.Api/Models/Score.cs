namespace DailyTrivia.Api.Models;

public class Score{
    public int Id {get;set;}
    public string UserId {get;set;} = string.Empty;
    public int QuestionId {get;set;}
    public bool IsCorrect {get;set;}
    public DateOnly DatePlayed {get;set;}
}