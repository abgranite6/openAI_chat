namespace OpenAiChat.Models;

public class OpenAiSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-3.5-turbo";
    public double Temperature { get; set; } = 0.25;
    public int MaxTokens { get; set; } = 1500;
    public int MaxRetryAttempts { get; set; } = 2;
}
