# Chat Controller - API Documentation

## Overview
Clean, production-ready REST API endpoint for conversational AI interactions.

---

## 📍 Endpoint

```
POST /api/chat
```

---

## 📥 Request

### Request Body
```json
{
  "conversationId": "string",
  "userInput": "string"
}
```

### Request Model
```csharp
public class ChatRequest
{
    public string ConversationId { get; set; }
    public string UserInput { get; set; }
}
```

### Field Descriptions

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `conversationId` | string | ✅ Yes | Unique identifier for the conversation session |
| `userInput` | string | ✅ Yes | User's message/question |

---

## 📤 Response

### Success Response (200 OK)
```json
{
  "answer": "This is the AI's response with token information included.\n\n[Tokens - Input: 250, Output: 120, Total: 370] [Conversation Total: 1540]"
}
```

### Response Model
```csharp
public class ChatResponse
{
    public string Answer { get; set; }
}
```

### Field Descriptions

| Field | Type | Description |
|-------|------|-------------|
| `answer` | string | AI-generated response including token usage information |

**Note**: The `summary` is **NOT** exposed to the frontend. It's managed internally by the service.

---

## ❌ Error Responses

### 400 Bad Request - Missing ConversationId
```json
{
  "error": "ConversationId is required"
}
```

### 400 Bad Request - Missing UserInput
```json
{
  "error": "UserInput is required"
}
```

---

## 🔄 How Follow-up Questions Work

The controller automatically handles follow-up questions through the service layer:

1. **Service auto-fetches summary** from Redis for the conversationId
2. **OpenAI uses the summary** to understand previous context
3. **Response is contextually aware** of the entire conversation
4. **Service updates summary** internally for future follow-ups

**Frontend doesn't need to manage any context!**

---

## 📝 Usage Examples

### Example 1: First Message in Conversation

**Request:**
```http
POST /api/chat
Content-Type: application/json

{
  "conversationId": "user-123-session-1",
  "userInput": "My name is Alice and I'm interested in web development"
}
```

**Response:**
```json
{
  "answer": "Nice to meet you, Alice! Web development is a great field. I'd be happy to help you with any questions about HTML, CSS, JavaScript, frameworks, or any other web development topics. What would you like to know?\n\n[Tokens - Input: 145, Output: 65, Total: 210] [Conversation Total: 210]"
}
```

**What happens behind the scenes:**
- ✅ Service fetches summary (none found - first message)
- ✅ Calls OpenAI with user input
- ✅ Saves user message: "My name is Alice..."
- ✅ Saves AI response
- ✅ Updates summary: "User's name is Alice. Interested in web development"
- ✅ Tracks tokens: 210 total

---

### Example 2: Follow-up Question (Context Maintained)

**Request:**
```http
POST /api/chat
Content-Type: application/json

{
  "conversationId": "user-123-session-1",
  "userInput": "What's my name?"
}
```

**Response:**
```json
{
  "answer": "Your name is Alice.\n\n[Tokens - Input: 135, Output: 25, Total: 160] [Conversation Total: 370]"
}
```

**What happens behind the scenes:**
- ✅ Service fetches summary: "User's name is Alice. Interested in web development"
- ✅ OpenAI receives context and answers correctly
- ✅ Saves messages
- ✅ Summary unchanged (no new context)
- ✅ Tracks tokens: 370 total (210 + 160)

---

### Example 3: Multi-turn Conversation

**Turn 1:**
```json
// Request
{
  "conversationId": "conv-456",
  "userInput": "I need help with Python"
}

// Response
{
  "answer": "I'd be happy to help you with Python! What specific aspect would you like assistance with?\n\n[Tokens - Input: 120, Output: 40, Total: 160] [Conversation Total: 160]"
}
```

**Turn 2:**
```json
// Request
{
  "conversationId": "conv-456",
  "userInput": "How do I read a file?"
}

// Response
{
  "answer": "To read a file in Python, you can use the built-in open() function...\n\n[Tokens - Input: 150, Output: 80, Total: 230] [Conversation Total: 390]"
}
```

**Turn 3:**
```json
// Request
{
  "conversationId": "conv-456",
  "userInput": "What did I ask about initially?"
}

// Response
{
  "answer": "You initially asked for help with Python, and then specifically about reading files.\n\n[Tokens - Input: 140, Output: 50, Total: 190] [Conversation Total: 580]"
}
```

**Context is automatically maintained across all turns!**

---

## 🏗️ Controller Implementation

### Clean & Simple
```csharp
[HttpPost]
public async Task<ActionResult<ChatResponse>> Post([FromBody] ChatRequest request)
{
    // Validation
    if (string.IsNullOrWhiteSpace(request.ConversationId))
        return BadRequest(new { error = "ConversationId is required" });

    if (string.IsNullOrWhiteSpace(request.UserInput))
        return BadRequest(new { error = "UserInput is required" });

    // Call service - it handles EVERYTHING
    var response = await _openAiService.GetResponseAsync(
        request.ConversationId,
        request.UserInput,
        summary: null,        // Auto-fetch from Redis
        knowledgeBase: null   // Not used in basic chat
    );

    // Return only the answer (NOT the summary)
    return Ok(new ChatResponse { Answer = response.Answer });
}
```

### What the Service Does Automatically:
1. ✅ Fetches conversation summary from Redis
2. ✅ Calls OpenAI API with full context
3. ✅ Saves user message
4. ✅ Saves AI response
5. ✅ Updates summary (if changed)
6. ✅ Tracks token usage
7. ✅ Returns response with token info

**Controller stays clean - all complexity in service layer!**

---

## 🔒 Security & Privacy

### Summary Not Exposed
```csharp
// ❌ What we DON'T return:
{
  "answer": "...",
  "summary": "User's name is Alice..."  // NOT exposed!
}

// ✅ What we DO return:
{
  "answer": "..."  // Only the answer
}
```

**Why?**
- Summary may contain sensitive user information
- Frontend doesn't need it (service manages context)
- Reduces response payload size
- Better separation of concerns

---

## 📊 Token Information

### Included in Answer
Every response includes token usage at the end:

```
[Tokens - Input: X, Output: Y, Total: Z] [Conversation Total: T]
```

**Example:**
```
[Tokens - Input: 250, Output: 120, Total: 370] [Conversation Total: 1540]
```

**Breakdown:**
- **Input**: Tokens used for prompt (system + context + user message)
- **Output**: Tokens used for AI response
- **Total**: Input + Output for this request
- **Conversation Total**: Accumulated tokens across entire conversation

---

## 🧪 Testing the Endpoint

### Using cURL
```bash
curl -X POST http://localhost:5136/api/chat \
  -H "Content-Type: application/json" \
  -d '{
    "conversationId": "test-123",
    "userInput": "Hello, how are you?"
  }'
```

### Using Postman
1. Method: `POST`
2. URL: `http://localhost:5136/api/chat`
3. Headers: `Content-Type: application/json`
4. Body (raw JSON):
   ```json
   {
     "conversationId": "test-123",
     "userInput": "Hello, how are you?"
   }
   ```

### Using Swagger
1. Navigate to `http://localhost:5136/swagger`
2. Find `POST /api/chat`
3. Click "Try it out"
4. Fill in the request body
5. Click "Execute"

---

## 🎯 Best Practices

### ConversationId Guidelines
```csharp
// ✅ Good: Unique per user session
"conversationId": "user-alice-session-20260403"

// ✅ Good: GUID format
"conversationId": "550e8400-e29b-41d4-a716-446655440000"

// ✅ Good: User ID + timestamp
"conversationId": "user123-1680523456"

// ❌ Bad: Same ID for all users
"conversationId": "chat"

// ❌ Bad: Not unique per conversation
"conversationId": "user-alice"  // Reused across sessions
```

### Frontend Implementation
```javascript
// Good practice: Generate unique conversation ID per session
const conversationId = `user-${userId}-${Date.now()}`;

async function sendMessage(userInput) {
  const response = await fetch('/api/chat', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      conversationId: conversationId,  // Same ID for entire session
      userInput: userInput
    })
  });
  
  const data = await response.json();
  return data.answer;
}

// First message
await sendMessage("My name is Alice");
// Response: "Nice to meet you, Alice!"

// Follow-up (automatic context)
await sendMessage("What's my name?");
// Response: "Your name is Alice."
```

---

## 🚀 Performance

### Response Time
- **Average**: 1-3 seconds (depends on OpenAI API)
- **Factors**: Prompt size, knowledge base, model speed

### Optimization
- ✅ Single OpenAI API call per request
- ✅ Async operations throughout
- ✅ Redis for fast context retrieval
- ✅ Minimal controller logic

---

## ✅ Features Summary

✅ **Clean API** - Simple request/response format  
✅ **Automatic Context** - Follow-ups work seamlessly  
✅ **No Frontend Complexity** - Service manages everything  
✅ **Token Tracking** - Included in every response  
✅ **Validation** - Input validation with clear errors  
✅ **Privacy** - Summary not exposed to frontend  
✅ **Production Ready** - Error handling, async, logging  

---

## 📂 Files

| File | Purpose |
|------|---------|
| `ChatController.cs` | API endpoint implementation |
| `ChatRequest.cs` | Request model |
| `ChatResponse.cs` | Response model (answer only) |

---

**Status**: ✅ **PRODUCTION READY**  
**Endpoint**: `POST /api/chat`  
**Version**: 1.0  
**Updated**: 2026-04-03
