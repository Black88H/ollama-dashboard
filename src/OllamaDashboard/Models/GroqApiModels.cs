namespace OllamaDashboard.Models;

public sealed class GroqChatRequest
{
    public string Model { get; set; } = string.Empty;
    public bool Stream { get; set; }
    public double Temperature { get; set; }
    public List<GroqMessage> Messages { get; set; } = new();
}

public sealed class GroqMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public sealed class GroqStreamChunk
{
    public List<GroqChoice>? Choices { get; set; }
}

public sealed class GroqChoice
{
    public GroqDelta? Delta { get; set; }
    public string? FinishReason { get; set; }
}

public sealed class GroqDelta
{
    public string? Content { get; set; }
}
