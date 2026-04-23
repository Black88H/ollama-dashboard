using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OllamaDashboard.Models;

namespace OllamaDashboard.Services;

public sealed class OllamaService : IOllamaService
{
    private readonly HttpClient _http;
    private readonly ISettingsService _settings;
    private readonly ILogger<OllamaService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OllamaService(
        HttpClient http,
        ISettingsService settings,
        ILogger<OllamaService> logger)
    {
        _http = http;
        _settings = settings;
        _logger = logger;

        // Large contexts can take 30+ minutes; keep_alive=-1 prevents mid-run model unloading.
        _http.Timeout = TimeSpan.FromMinutes(30);
    }

    private Uri BuildUri(string path)
    {
        var baseUrl = _settings.Current.OllamaBaseUrl.TrimEnd('/');
        return new Uri($"{baseUrl}{path}");
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, BuildUri("/api/tags"));
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ollama availability check failed");
            return false;
        }
    }

    public async Task<IReadOnlyList<OllamaModelInfo>> GetInstalledModelsAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<OllamaTagsResponse>(
                BuildUri("/api/tags"), JsonOpts, ct);
            return response?.Models ?? new List<OllamaModelInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch installed Ollama models");
            return Array.Empty<OllamaModelInfo>();
        }
    }

    public async IAsyncEnumerable<string> StreamChatAsync(
        IEnumerable<ChatMessage> conversation,
        string? modelOverride = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var model = modelOverride ?? _settings.Current.SelectedModel;
        var request = new OllamaChatRequest
        {
            Model = model,
            Stream = true,
            Messages = conversation.Select(m => new OllamaChatMessage
            {
                Role = m.Role switch
                {
                    ChatRole.User      => "user",
                    ChatRole.Assistant => "assistant",
                    ChatRole.System    => "system",
                    _ => "user"
                },
                Content = m.Content
            }).ToList(),
            Options = new OllamaOptions
            {
                Temperature = _settings.Current.Temperature,
                NumCtx = _settings.Current.ContextWindow
            }
        };

        using var httpReq = new HttpRequestMessage(HttpMethod.Post, BuildUri("/api/chat"))
        {
            Content = JsonContent.Create(request, options: JsonOpts)
        };

        using var httpResp = await _http.SendAsync(
            httpReq, HttpCompletionOption.ResponseHeadersRead, ct);
        httpResp.EnsureSuccessStatusCode();

        await using var stream = await httpResp.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (!reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line)) continue;

            OllamaChatStreamChunk? chunk;
            try
            {
                chunk = JsonSerializer.Deserialize<OllamaChatStreamChunk>(line, JsonOpts);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse Ollama stream chunk: {Line}", line);
                continue;
            }

            if (chunk?.Message?.Content is { Length: > 0 } content)
            {
                yield return content;
            }

            if (chunk?.Done == true) break;
        }
    }

    public async Task<string> ChatAsync(
        IEnumerable<ChatMessage> conversation,
        string? modelOverride = null,
        CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        await foreach (var chunk in StreamChatAsync(conversation, modelOverride, ct))
        {
            sb.Append(chunk);
        }
        return sb.ToString();
    }
}
