using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OllamaDashboard.Models;

namespace OllamaDashboard.Services;

public sealed class GroqService : IGroqService
{
    private const string ApiBase = "https://api.groq.com/openai/v1";

    private readonly HttpClient _http;
    private readonly ISettingsService _settings;
    private readonly ILogger<GroqService> _logger;

    // snake_case serialization to match Groq's OpenAI-compatible API
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_settings.Current.GroqApiKey);

    public GroqService(
        HttpClient http,
        ISettingsService settings,
        ILogger<GroqService> logger)
    {
        _http = http;
        _settings = settings;
        _logger = logger;
        _http.Timeout = TimeSpan.FromMinutes(10);
    }

    public async IAsyncEnumerable<string> StreamChatAsync(
        IEnumerable<ChatMessage> conversation,
        string? modelOverride = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var model = modelOverride ?? _settings.Current.GroqModel;

        var request = new GroqChatRequest
        {
            Model = model,
            Stream = true,
            Temperature = _settings.Current.Temperature,
            Messages = conversation.Select(m => new GroqMessage
            {
                Role = m.Role switch
                {
                    ChatRole.User      => "user",
                    ChatRole.Assistant => "assistant",
                    ChatRole.System    => "system",
                    _ => "user"
                },
                Content = m.Content
            }).ToList()
        };

        using var httpReq = new HttpRequestMessage(HttpMethod.Post, $"{ApiBase}/chat/completions")
        {
            Content = JsonContent.Create(request, options: JsonOpts)
        };
        httpReq.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _settings.Current.GroqApiKey);

        using var httpResp = await _http.SendAsync(
            httpReq, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!httpResp.IsSuccessStatusCode)
        {
            var body = await httpResp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Groq API Fehler {(int)httpResp.StatusCode}: {body}");
        }

        await using var stream = await httpResp.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        // Groq uses OpenAI-compatible SSE: lines prefixed with "data: "
        while (!reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!line.StartsWith("data: ")) continue;

            var data = line["data: ".Length..];
            if (data == "[DONE]") break;

            GroqStreamChunk? chunk;
            try
            {
                chunk = JsonSerializer.Deserialize<GroqStreamChunk>(data, JsonOpts);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse Groq SSE chunk: {Data}", data);
                continue;
            }

            var content = chunk?.Choices?.FirstOrDefault()?.Delta?.Content;
            if (!string.IsNullOrEmpty(content))
                yield return content;
        }
    }

    public async Task<string> ChatAsync(
        IEnumerable<ChatMessage> conversation,
        string? modelOverride = null,
        CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        await foreach (var chunk in StreamChatAsync(conversation, modelOverride, ct))
            sb.Append(chunk);
        return sb.ToString();
    }
}
