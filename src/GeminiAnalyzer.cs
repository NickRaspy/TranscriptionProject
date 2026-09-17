using System.Text;
using System.Text.Json;

namespace TranscriptMvp;

public sealed class GeminiAnalyzer(HttpClient http, string key, string model)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly JsonElement ResponseSchema = CreateResponseSchema();
    private readonly string _key = !string.IsNullOrWhiteSpace(key) ? key : throw new ArgumentException("Gemini API key is empty.", nameof(key));
    private readonly string _model = !string.IsNullOrWhiteSpace(model) ? model : throw new ArgumentException("Gemini model is empty.", nameof(model));

    public async Task<ConversationResult> AnalyzeAsync(string id, string source, string? correction = null)
    {
        var payload = new
        {
            systemInstruction = new { parts = new[] { new { text = AnalysisPrompt.SystemInstruction } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = AnalysisPrompt.UserMessage(id, source, correction) } } } },
            generationConfig = new { temperature = 0, responseMimeType = "application/json", responseSchema = ResponseSchema }
        };
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(_model)}:generateContent";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("x-goog-api-key", _key);
        request.Content = new StringContent(JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json");

        using var response = await http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            var detail = GetErrorMessage(body)?.Replace(_key, "[redacted]", StringComparison.Ordinal);
            var suffix = detail is null ? "." : $": {detail}";
            throw new HttpRequestException($"Gemini request for {_model} failed with HTTP {(int)response.StatusCode}{suffix}");
        }

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
        foreach (var item in result.Evidence ?? [])
        {
            var quote = item.Quote?.TrimEnd();
            if (quote is null) continue;
            var suffixLength = quote.EndsWith("...", StringComparison.Ordinal) ? 3 : quote.EndsWith('…') ? 1 : 0;
            if (suffixLength == 0) continue;
            var excerpt = quote[..^suffixLength].TrimEnd();
            if (excerpt.Length >= 20 && source.Contains(excerpt, StringComparison.Ordinal))
                item.Quote = excerpt;
        }
        return result.TranscriptId != id
            ? throw new InvalidDataException($"Gemini returned wrong transcript ID for {id}.")
            : result;
    }

    private static string? GetErrorMessage(string body)
    {
        try
        {
            using var response = JsonDocument.Parse(body);
            return response.RootElement.GetProperty("error").GetProperty("message").GetString();
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }

    private static JsonElement CreateResponseSchema()
    {
        using var document = JsonDocument.Parse("""
            {
              "type": "OBJECT",
              "properties": {
                "transcriptId": { "type": "STRING" },
                "conversationDate": { "type": "STRING" },
                "client": { "type": "STRING" },
                "outcome": { "type": "STRING" },
                "nextStep": {
                  "type": "OBJECT",
                  "properties": {
                    "action": { "type": "STRING" },
                    "responsible": { "type": "STRING" },
                    "date": { "type": "STRING", "nullable": true },
                    "dateText": { "type": "STRING" },
                    "dateIsAmbiguous": { "type": "BOOLEAN" }
                  },
                  "required": ["action", "responsible", "date", "dateText", "dateIsAmbiguous"]
                },
                "additionalAgreedActions": {
                  "type": "ARRAY",
                  "items": {
                    "type": "OBJECT",
                    "properties": {
                      "action": { "type": "STRING" },
                      "responsible": { "type": "STRING" },
                      "date": { "type": "STRING", "nullable": true },
                      "dateText": { "type": "STRING" },
                      "dateIsAmbiguous": { "type": "BOOLEAN" }
                    },
                    "required": ["action", "responsible", "date", "dateText", "dateIsAmbiguous"]
                  }
                },
                "clientNeeds": { "type": "ARRAY", "items": { "type": "STRING" } },
                "risks": { "type": "ARRAY", "items": { "type": "STRING" } },
                "managerMistakes": { "type": "ARRAY", "items": { "type": "STRING" } },
                "managerAttention": { "type": "ARRAY", "items": { "type": "STRING" } },
                "missingOrAmbiguousInformation": { "type": "ARRAY", "items": { "type": "STRING" } },
                "evidence": {
                  "type": "ARRAY",
                  "items": {
                    "type": "OBJECT",
                    "properties": {
                      "claim": { "type": "STRING" },
                      "quote": { "type": "STRING" }
                    },
                    "required": ["claim", "quote"]
                  }
                }
              },
              "required": [
                "transcriptId", "conversationDate", "client", "outcome", "nextStep",
                "additionalAgreedActions", "clientNeeds", "risks", "managerMistakes",
                "managerAttention", "missingOrAmbiguousInformation", "evidence"
              ]
            }
            """);
        return document.RootElement.Clone();
    }
}
