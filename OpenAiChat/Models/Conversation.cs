namespace OpenAiChat.Models;

public class Conversation
{
    public string ConversationId { get; set; } = string.Empty;
    public List<Message> Messages { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
    public int TotalTokensUsed { get; set; } = 0;
}