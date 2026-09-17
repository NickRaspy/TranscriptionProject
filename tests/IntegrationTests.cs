using System.Net;
using System.Text;
using System.Text.Json;
using NUnit.Framework;
using TranscriptMvp;

namespace TranscriptMvp.Tests;

[TestFixture]
public sealed class IntegrationTests
{
    [Test]
    public void CommandLineUsesDefaultsAndResolvesPaths()
    {
        var root = Path.GetTempPath();
        var defaults = CommandLineOptions.Parse(["--demo"], root);
        var custom = CommandLineOptions.Parse(["--input", "notes.txt", "--output", "out"], root);
        var gemini = CommandLineOptions.Parse(["--gemini"], root);

        Assert.Multiple(() =>
        {
            Assert.That(defaults.Demo, Is.True);
            Assert.That(gemini.Gemini, Is.True);
            Assert.That(defaults.InputPath, Is.EqualTo(Path.GetFullPath(Path.Combine(root, "data", "transcripts"))));
            Assert.That(defaults.OutputPath, Is.EqualTo(Path.GetFullPath(root)));
            Assert.That(custom.InputPath, Is.EqualTo(Path.GetFullPath("notes.txt")));
            Assert.That(custom.OutputPath, Is.EqualTo(Path.GetFullPath("out")));
        });
    }

    [TestCase("--input")]
    [TestCase("--input --demo")]
    [TestCase("--demo --demo")]
    [TestCase("--demo --gemini")]
    [TestCase("--unknown")]
    public void CommandLineRejectsInvalidArguments(string arguments)
    {
        Assert.Throws<ArgumentException>(() => CommandLineOptions.Parse(arguments.Split(' '), Path.GetTempPath()));
    }

    [Test]
    public async Task ApiClientSendsExpectedRequestAndReadsResult()
    {
        var conversation = SampleConversation("case-1");
        var content = JsonSerializer.Serialize(conversation, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var envelope = JsonSerializer.Serialize(new { choices = new[] { new { message = new { content } } } });
        using var handler = new StubHandler(HttpStatusCode.OK, envelope);
        using var http = new HttpClient(handler);
        var analyzer = new OpenAiAnalyzer(http, "test-key", "https://example.test/v1/", "test-model");

        var result = await analyzer.AnalyzeAsync("case-1", "Пример разговора");
        using var requestJson = JsonDocument.Parse(handler.Body!);

        Assert.Multiple(() =>
        {
            Assert.That(result.TranscriptId, Is.EqualTo("case-1"));
            Assert.That(handler.RequestUri, Is.EqualTo("https://example.test/v1/chat/completions"));
            Assert.That(handler.Authorization, Is.EqualTo("Bearer test-key"));
            Assert.That(requestJson.RootElement.GetProperty("model").GetString(), Is.EqualTo("test-model"));
            Assert.That(requestJson.RootElement.GetProperty("messages")[1].GetProperty("content").GetString(), Does.Contain("Пример разговора"));
        });
    }

    [Test]
    public void ApiClientRejectsMismatchedTranscriptId()
    {
        var content = JsonSerializer.Serialize(SampleConversation("other"), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var envelope = JsonSerializer.Serialize(new { choices = new[] { new { message = new { content } } } });
        using var handler = new StubHandler(HttpStatusCode.OK, envelope);
        using var http = new HttpClient(handler);
        var analyzer = new OpenAiAnalyzer(http, "test-key", "https://example.test/v1", "test-model");

        Assert.ThrowsAsync<InvalidDataException>(async () => await analyzer.AnalyzeAsync("case-1", "source"));
    }

    [Test]
    public async Task GeminiClientSendsJsonRequestAndReadsResult()
    {
        var content = JsonSerializer.Serialize(SampleConversation("case-1"), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var envelope = JsonSerializer.Serialize(new
        {
            candidates = new[]
            {
                new { finishReason = "STOP", content = new { parts = new[] { new { text = content } } } }
            }
        });
        using var handler = new StubHandler(HttpStatusCode.OK, envelope);
        using var http = new HttpClient(handler);
        var analyzer = new GeminiAnalyzer(http, "test-key", "gemini-2.5-flash");

        var result = await analyzer.AnalyzeAsync("case-1", "Пример разговора");
        using var requestJson = JsonDocument.Parse(handler.Body!);

        Assert.Multiple(() =>
        {
            Assert.That(result.TranscriptId, Is.EqualTo("case-1"));
            Assert.That(handler.RequestUri, Is.EqualTo("https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent"));
            Assert.That(handler.GeminiKey, Is.EqualTo("test-key"));
            Assert.That(requestJson.RootElement.GetProperty("generationConfig").GetProperty("responseMimeType").GetString(), Is.EqualTo("application/json"));
            Assert.That(requestJson.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString(), Does.Contain("Пример разговора"));
        });
    }

    [Test]
    public void GeminiClientRejectsIncompleteResponse()
    {
        using var handler = new StubHandler(HttpStatusCode.OK, """{"candidates":[{"finishReason":"MAX_TOKENS"}]}""");
        using var http = new HttpClient(handler);
        var analyzer = new GeminiAnalyzer(http, "test-key", "gemini-2.5-flash");

        Assert.ThrowsAsync<InvalidDataException>(async () => await analyzer.AnalyzeAsync("case-1", "source"));
    }

    private static ConversationResult SampleConversation(string id) => new()
    {
        TranscriptId = id,
        ConversationDate = "2026-09-08",
        Client = "Клиент",
        Outcome = "Итог",
        NextStep = new AgreedAction { Action = "Связаться", Responsible = "Менеджер", Date = "2026-09-09", DateText = "завтра", DateIsAmbiguous = false },
        AdditionalAgreedActions = [],
        ClientNeeds = [],
        Risks = [],
        ManagerMistakes = [],
        ManagerAttention = [],
        MissingOrAmbiguousInformation = [],
        Evidence = [new EvidenceItem { Claim = "Итог", Quote = "Пример" }]
    };

    private sealed class StubHandler(HttpStatusCode status, string responseBody) : HttpMessageHandler
    {
        public string? RequestUri { get; private set; }
        public string? Authorization { get; private set; }
        public string? GeminiKey { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri?.ToString();
            Authorization = request.Headers.Authorization?.ToString();
            GeminiKey = request.Headers.TryGetValues("x-goog-api-key", out var values) ? values.Single() : null;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(responseBody, Encoding.UTF8, "application/json") };
        }
    }
}
