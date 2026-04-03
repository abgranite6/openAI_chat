public class ConversationHistory
{
    public string ConversationId { get; set; }
    public List<Message> Messages { get; set; } = new();
    public string Summary { get; set; }
}