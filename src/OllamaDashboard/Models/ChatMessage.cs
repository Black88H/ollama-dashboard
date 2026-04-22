using CommunityToolkit.Mvvm.ComponentModel;

namespace OllamaDashboard.Models;

public enum ChatRole
{
    User,
    Assistant,
    System
}

/// <summary>
/// Represents a single message in a chat conversation.
/// ObservableObject so streaming updates to Content trigger UI re-render.
/// </summary>
public partial class ChatMessage : ObservableObject
{
    [ObservableProperty]
    private string _content = string.Empty;

    [ObservableProperty]
    private bool _isStreaming;

    public ChatRole Role { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public bool IsUser => Role == ChatRole.User;
    public bool IsAssistant => Role == ChatRole.Assistant;
    public bool IsSystem => Role == ChatRole.System;
}
