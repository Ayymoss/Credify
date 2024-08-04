using System.Text.Json.Serialization;

namespace Credify.Models.ApiModels;

public class Trivia
{
    [JsonPropertyName("response_code")]
    public int ResponseCode { get; set; }

    [JsonPropertyName("results")]
    public List<TriviaResult> Results { get; set; }
}

public class TriviaResult
{
    [JsonPropertyName("correct_answer")]
    public string CorrectAnswer { get; set; }
    
    [JsonPropertyName("incorrect_answers")]
    public List<string> IncorrectAnswers { get; set; }

    [JsonPropertyName("question")]
    public string Question { get; set; }

}
