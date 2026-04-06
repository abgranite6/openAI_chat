using Microsoft.AspNetCore.Mvc;
using OpenAiChat.Interfaces;
using OpenAiChat.Models;

namespace OpenAiChat.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IOpenAiService _openAiService;

    public ChatController(IOpenAiService openAiService)
    {
        _openAiService = openAiService;
    }

    [HttpPost]
    public async Task<ActionResult<ChatResponse>> Post([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ConversationId))
        {
            return BadRequest(new { error = "ConversationId is required" });
        }

        if (string.IsNullOrWhiteSpace(request.UserInput))
        {
            return BadRequest(new { error = "UserInput is required" });
        }

        // Call OpenAI service - it handles everything:
        // - Auto-fetches summary from Redis
        // - Calls OpenAI API
        // - Saves messages
        // - Updates summary
        // - Tracks tokens
        var response = await _openAiService.GetResponseAsync(
            request.ConversationId,
            request.UserInput,
            knowledgeBase: null
        );

        // Return only the answer - do NOT expose summary to frontend
        return Ok(new ChatResponse
        {
            Answer = response.Answer
        });
    }
}