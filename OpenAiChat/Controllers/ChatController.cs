using Microsoft.AspNetCore.Mvc;
using OpenAiChat.Interfaces;
using OpenAiChat.Models;

namespace OpenAiChat.Controllers;

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
}