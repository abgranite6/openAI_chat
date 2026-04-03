using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IOpenAiService _openAiService;
    private readonly IRedisService _redisService;

    public ChatController(IOpenAiService openAiService, IRedisService redisService)
    {
        _openAiService = openAiService;
        _redisService = redisService;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] ChatRequest request)
    {
        var history = await _redisService.GetConversationHistoryAsync(request.ConversationId);
        var response = await _openAiService.GetResponseAsync(request.ConversationId, request.UserInput, history.Summary);

        // Add user message
        history.Messages.Add(new Message
        {
            ConversationId = request.ConversationId,
            Content = request.UserInput,
            MessageOrder = history.Messages.Count + 1
        });

        // Add AI response
        history.Messages.Add(new Message
        {
            ConversationId = request.ConversationId,
            Content = response.Answer,
            MessageOrder = history.Messages.Count + 1
        });

        // Update summary if provided
        if (!string.IsNullOrEmpty(response.Summary))
        {
            history.Summary = response.Summary;
        }

        await _redisService.SaveConversationHistoryAsync(history);

        return Ok(response);
    }
}