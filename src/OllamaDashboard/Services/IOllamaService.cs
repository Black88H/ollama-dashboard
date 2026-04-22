using OllamaDashboard.Models;

namespace OllamaDashboard.Services;

public interface IOllamaService
{
    /// <summary>Checks whether the Ollama daemon is reachable at the configured URL.</summary>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);

    /// <summary>Lists all models that are currently pulled locally.</summary>
    Task<IReadOnlyList<OllamaModelInfo>> GetInstalledModelsAsync(CancellationToken ct = default);

    /// <summary>
    /// Streams a chat completion. Yields one chunk per token/segment as emitted by the server.
    /// The caller is responsible for appending chunks to the UI.
    /// </summary>
    IAsyncEnumerable<string> StreamChatAsync(
        IEnumerable<ChatMessage> conversation,
        string? modelOverride = null,
        CancellationToken ct = default);

    /// <summary>
    /// Convenience: runs a chat completion and returns the full concatenated text.
    /// </summary>
    Task<string> ChatAsync(
        IEnumerable<ChatMessage> conversation,
        string? modelOverride = null,
        CancellationToken ct = default);
}
