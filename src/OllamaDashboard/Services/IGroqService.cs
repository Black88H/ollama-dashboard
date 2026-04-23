using OllamaDashboard.Models;

namespace OllamaDashboard.Services;

public interface IGroqService
{
    /// <summary>True when a non-empty API key is configured.</summary>
    bool IsConfigured { get; }

    IAsyncEnumerable<string> StreamChatAsync(
        IEnumerable<ChatMessage> conversation,
        string? modelOverride = null,
        CancellationToken ct = default);

    Task<string> ChatAsync(
        IEnumerable<ChatMessage> conversation,
        string? modelOverride = null,
        CancellationToken ct = default);
}
