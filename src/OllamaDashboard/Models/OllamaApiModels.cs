using System.Text.Json.Serialization;

namespace OllamaDashboard.Models;

// ---------------------------------------------------------------------------
// Ollama REST API DTOs
// Docs: https://github.com/ollama/ollama/blob/main/docs/api.md
// ---------------------------------------------------------------------------

/// <summary>GET /api/tags → list of locally installed models.</summary>
public sealed class OllamaTagsResponse
{
    [JsonPropertyName("models")]
    public List<OllamaModelInfo> Models { get; set; } = new();
}

public sealed class OllamaModelInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("modified_at")]
    public DateTime ModifiedAt { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("digest")]
    public string Digest { get; set; } = string.Empty;

    [JsonPropertyName("details")]
    public OllamaModelDetails? Details { get; set; }
}

public sealed class OllamaModelDetails
{
    [JsonPropertyName("family")]
    public string? Family { get; set; }

    [JsonPropertyName("parameter_size")]
    public string? ParameterSize { get; set; }

    [JsonPropertyName("quantization_level")]
    public string? QuantizationLevel { get; set; }
}

// --- POST /api/chat (streaming NDJSON) ------------------------------------

public sealed class OllamaChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<OllamaChatMessage> Messages { get; set; } = new();

    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = true;

    [JsonPropertyName("options")]
    public OllamaOptions? Options { get; set; }
}

public sealed class OllamaChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user"; // "user" | "assistant" | "system"

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

public sealed class OllamaOptions
{
    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    [JsonPropertyName("top_p")]
    public double? TopP { get; set; }

    [JsonPropertyName("num_ctx")]
    public int? NumCtx { get; set; }

    [JsonPropertyName("num_predict")]
    public int? NumPredict { get; set; }
}

public sealed class OllamaChatStreamChunk
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("message")]
    public OllamaChatMessage? Message { get; set; }

    [JsonPropertyName("done")]
    public bool Done { get; set; }

    [JsonPropertyName("done_reason")]
    public string? DoneReason { get; set; }

    [JsonPropertyName("total_duration")]
    public long? TotalDuration { get; set; }

    [JsonPropertyName("eval_count")]
    public int? EvalCount { get; set; }
}
