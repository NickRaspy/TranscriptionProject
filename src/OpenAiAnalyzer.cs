using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace TranscriptMvp;

public sealed class OpenAiAnalyzer(HttpClient http, string key, string baseUrl, string model)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly string _key = !string.IsNullOrWhiteSpace(key) ? key : throw new ArgumentException("API key is empty.", nameof(key));
    private readonly string _baseUrl = !string.IsNullOrWhiteSpace(baseUrl) ? baseUrl.TrimEnd('/') : throw new ArgumentException("API base URL is empty.", nameof(baseUrl));
    private readonly string _model = !string.IsNullOrWhiteSpace(model) ? model : throw new ArgumentException("Model is empty.", nameof(model));

    public async Task<ConversationResult> AnalyzeAsync(string id, string source)
    {
        var payload = new
        {
            model = _model,
            temperature = 0,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = AnalysisPrompt.SystemInstruction },
                new { role = "user", content = AnalysisPrompt.UserMessage(id, source) }
            }
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _key);
        request.Content = new StringContent(JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json");

        using var response = await http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"API request failed with HTTP {(int)response.StatusCode}.");

        using var envelope = JsonDocument.Parse(body);
        var content = envelope.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        var result = JsonSerializer.Deserialize<ConversationResult>(content ?? "", Json)
            ?? throw new InvalidDataException($"Empty API result for {id}.");
        return result.TranscriptId != id ? throw new InvalidDataException($"API returned wrong transcript ID for {id}.") : result;
    }
}
