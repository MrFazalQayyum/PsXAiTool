using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PsXAiTool.Core.DTOs;
using PsXAiTool.Core.Entities;
using PsXAiTool.Core.Interfaces;
using PsXAiTool.Core.Settings;

namespace PsXAiTool.Infrastructure.Services;

public class ClaudeService(
    HttpClient httpClient,
    IOptions<AppSettings> settings,
    ILogger<ClaudeService> logger) : IClaudeService
{
    private readonly AppSettings _settings = settings.Value;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public async Task<List<ExtractedSignal>> ExtractSignalsAsync(string headline, string? content)
    {
        var prompt = $"""
            You are a Pakistan stock market analyst. Extract trading signals from this news.
            Return a JSON array of signal objects with these exact fields:
            signal_type, entities (list of PSX ticker symbols), sectors (list of sector names),
            direction (Bullish/Bearish/Neutral), confidence (0.0-1.0), summary (1 sentence),
            historical_note (optional context).
            If no relevant signal, return an empty array [].

            Headline: {headline}
            Content: {content ?? ""}

            Return ONLY valid JSON, no markdown.
            """;

        var response = await CallClaudeAsync(_settings.HaikuModel, prompt);
        if (response is null) return new List<ExtractedSignal>();

        try
        {
            var signals = JsonSerializer.Deserialize<List<ExtractedSignal>>(response, JsonOptions);
            return signals ?? new List<ExtractedSignal>();
        }
        catch (JsonException ex)
        {
            logger.LogWarning("Failed to parse Claude signal response: {Error}", ex.Message);
            return new List<ExtractedSignal>();
        }
    }

    public async Task<string> GenerateBriefingAsync(List<Signal> recentSignals, decimal? overallAccuracy)
    {
        var signalSummary = string.Join("\n", recentSignals.Select(s =>
            $"- [{s.Direction}] {s.SignalType}: {s.Summary} (confidence: {s.Confidence:P0})"));

        var accuracyNote = overallAccuracy.HasValue
            ? $"Historical accuracy: {overallAccuracy:P0}"
            : "Historical accuracy: Insufficient data";

        var prompt = $"""
            You are a Pakistan stock market analyst. Write a concise 200-word morning market briefing
            for PSX investors based on these recent signals:

            {signalSummary}

            {accuracyNote}

            Focus on actionable insights. Be direct and professional.
            """;

        var content = await CallClaudeAsync(_settings.SonnetModel, prompt);
        return content ?? "Unable to generate briefing at this time.";
    }

    private async Task<string?> CallClaudeAsync(string model, string userPrompt)
    {
        var body = new
        {
            model,
            max_tokens = 1024,
            messages = new[] { new { role = "user", content = userPrompt } }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-api-key", _settings.AnthropicApiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        try
        {
            var httpResponse = await httpClient.SendAsync(request);
            httpResponse.EnsureSuccessStatusCode();

            using var doc = await JsonDocument.ParseAsync(await httpResponse.Content.ReadAsStreamAsync());
            return doc.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString();
        }
        catch (Exception ex)
        {
            logger.LogError("Claude API call failed: {Error}", ex.Message);
            return null;
        }
    }
}
