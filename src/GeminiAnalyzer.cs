using System.Text;
using System.Text.Json;

namespace TranscriptMvp;

public sealed class GeminiAnalyzer(HttpClient http, string key, string model)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly string _key = !string.IsNullOrWhiteSpace(key) ? key : throw new ArgumentException("Gemini API key is empty.", nameof(key));
    private readonly string _model = !string.IsNullOrWhiteSpace(model) ? model : throw new ArgumentException("Gemini model is empty.", nameof(model));

    public async Task<ConversationResult> AnalyzeAsync(string id, string source)
    {
        var payload = new
        {
            systemInstruction = new { parts = new[] { new { text = AnalysisPrompt.SystemInstruction } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = AnalysisPrompt.UserMessage(id, source) } } } },
            generationConfig = new { temperature = 0, responseMimeType = "application/json" }
        };
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(_model)}:generateContent";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("x-goog-api-key", _key);
        request.Content = new StringContent(JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json");

        using var response = await http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Gemini request failed with HTTP {(int)response.StatusCode}.");

        using var envelope = JsonDocument.Parse(body);
        if (!envelope.RootElement.TryGetProperty("candidates", out var candidates) ||
            candidates.ValueKind != JsonValueKind.Array || candidates.GetArrayLength() == 0)
            throw new InvalidDataException($"Gemini returned no result for {id}.");
        var candidate = candidates[0];
        if (candidate.TryGetProperty("finishReason", out var reason) &&
            reason.GetString() != "STOP")
            throw new InvalidDataException($"Gemini stopped without a complete result for {id}: {reason.GetString()}.");

        var parts = candidate.GetProperty("content").GetProperty("parts");
        var content = string.Concat(parts.EnumerateArray().Select(part => part.GetProperty("text").GetString()));
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidDataException($"Gemini returned empty text for {id}.");
        var result = JsonSerializer.Deserialize<ConversationResult>(content, Json)
            ?? throw new InvalidDataException($"Gemini returned empty JSON for {id}.");
        return result.TranscriptId != id
            ? throw new InvalidDataException($"Gemini returned wrong transcript ID for {id}.")
            : result;
    }
}
