using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace TranscriptMvp;

public sealed class OpenAiAnalyzer(HttpClient http, string key, string baseUrl, string model)
{
    private const string SystemInstruction = """
        Ты анализируешь разговор интегратора Saby с B2B-клиентом. Используй только транскрипт и предоставленный общий контекст. Не придумывай участников, сроки, договорённости, суммы или свойства продукта. Отличай просьбу, предложение, отказ и подтверждённую договорённость. Не называй действие согласованным, пока другая сторона его не подтвердила. Явно отмечай отсутствующие и неоднозначные данные. Сохраняй реальные возражения клиента. Ошибки менеджера оценивай только по конкретным репликам. Для каждого существенного вывода добавь короткую точную цитату в evidence. Ответь только валидным JSON без markdown и дополнительного текста.
        Правила дат: разрешай относительные сроки от даты разговора, сверяй день недели; точный день указывай лишь если он однозначен. Для Екатеринбурга используй UTC+05:00. Если назван месяц или «после двадцатого января», date=null, dateIsAmbiguous=true, исходная фраза в dateText. Если действий несколько, nextStep — ближайшее согласованное действие, остальные в additionalAgreedActions. Не записывай условное предложение как безусловное обязательство.
        JSON-объект должен содержать transcriptId, conversationDate (YYYY-MM-DD), client, outcome, nextStep, additionalAgreedActions, clientNeeds, risks, managerMistakes, managerAttention, missingOrAmbiguousInformation, evidence. У каждого действия поля action, responsible, date (ISO 8601 или null), dateText, dateIsAmbiguous. evidence — массив объектов claim и quote. Все массивы обязательны, даже если пустые.
        """;

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
                new { role = "system", content = SystemInstruction },
                new { role = "user", content = $"Общий контекст: интегратор внедряет Saby (ЭДО, учёт, CRM, маркировка и другие модули). ID транскрипта: {id}.\n\n{source}" }
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
