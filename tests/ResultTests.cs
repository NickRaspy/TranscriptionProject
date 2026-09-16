using System.Text.Json;
using NUnit.Framework;
using TranscriptMvp;

namespace TranscriptMvp.Tests;

[TestFixture]
public sealed class ResultTests
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    [Test]
    public void AllCaseResultsPassValidationAgainstOriginalTranscripts()
    {
        ResultValidator.Validate(LoadBatch(), LoadTranscripts());
    }

    [Test]
    public void JsonRoundTripRetainsAllConversations()
    {
        var json = JsonSerializer.Serialize(LoadBatch());
        var restored = JsonSerializer.Deserialize<ResultBatch>(json, Json);

        Assert.That(restored, Is.Not.Null);
        ResultValidator.Validate(restored!, LoadTranscripts());
        Assert.That(restored!.Conversations.Select(x => x.TranscriptId), Is.EquivalentTo(["1", "2", "3", "4"]));
    }

    [TestCase("1", "2026-09-08")]
    [TestCase("2", "2026-09-09")]
    [TestCase("3", "2026-09-07")]
    public void ImmediateActionDatesMatchConversationRelativeTerms(string id, string expected)
    {
        var action = LoadBatch().Conversations.Single(x => x.TranscriptId == id).NextStep;
        Assert.That(action.Date, Is.EqualTo(expected));
        Assert.That(action.DateIsAmbiguous, Is.False);
    }

    [Test]
    public void LaterExactDatesAndTimezonesArePreserved()
    {
        var batch = LoadBatch();
        Assert.That(Conversation(batch, "1").AdditionalAgreedActions[0].Date, Is.EqualTo("2026-09-10T15:00:00+05:00"));
        Assert.That(Conversation(batch, "2").AdditionalAgreedActions[0].Date, Is.EqualTo("2026-09-14"));
        Assert.That(Conversation(batch, "3").AdditionalAgreedActions[0].Date, Is.EqualTo("2026-09-11T11:30:00"));
    }

    [Test]
    public void DecemberAndAfterJanuaryTwentiethHaveNoInventedDay()
    {
        var actions = Conversation(LoadBatch(), "4");
        Assert.Multiple(() =>
        {
            Assert.That(actions.NextStep.Date, Is.Null);
            Assert.That(actions.NextStep.DateIsAmbiguous, Is.True);
            Assert.That(actions.AdditionalAgreedActions[0].Date, Is.Null);
            Assert.That(actions.AdditionalAgreedActions[0].DateIsAmbiguous, Is.True);
        });
    }

    [Test]
    public void AmbiguousActionWithPreciseDateIsRejected()
    {
        var batch = LoadBatch();
        Conversation(batch, "4").NextStep.Date = "2026-12-01";
        Assert.Throws<InvalidDataException>(() => ResultValidator.Validate(batch, LoadTranscripts()));
    }

    [Test]
    public void PreciseActionWithoutDateIsRejected()
    {
        var batch = LoadBatch();
        Conversation(batch, "1").NextStep.Date = null;
        Assert.Throws<InvalidDataException>(() => ResultValidator.Validate(batch, LoadTranscripts()));
    }

    [Test]
    public void WrongYekaterinburgOffsetIsRejected()
    {
        var batch = LoadBatch();
        Conversation(batch, "1").AdditionalAgreedActions[0].Date = "2026-09-10T15:00:00+03:00";
        Assert.Throws<InvalidDataException>(() => ResultValidator.Validate(batch, LoadTranscripts()));
    }

    [Test]
    public void InventedQuoteIsRejected()
    {
        var batch = LoadBatch();
        Conversation(batch, "1").Evidence[0].Quote = "Этой реплики нет в разговоре";
        Assert.Throws<InvalidDataException>(() => ResultValidator.Validate(batch, LoadTranscripts()));
    }

    [Test]
    public void EmptyRequiredTextIsRejected()
    {
        var batch = LoadBatch();
        Conversation(batch, "1").Outcome = " ";
        Assert.Throws<InvalidDataException>(() => ResultValidator.Validate(batch, LoadTranscripts()));
    }

    [Test]
    public void MissingRequiredListIsRejected()
    {
        var batch = LoadBatch();
        Conversation(batch, "2").MissingOrAmbiguousInformation = null!;
        Assert.Throws<InvalidDataException>(() => ResultValidator.Validate(batch, LoadTranscripts()));
    }

    [Test]
    public void DuplicateTranscriptIdIsRejected()
    {
        var batch = LoadBatch();
        Conversation(batch, "2").TranscriptId = "1";
        Assert.Throws<InvalidDataException>(() => ResultValidator.Validate(batch, LoadTranscripts()));
    }

    [Test]
    public void ReportEscapesTranscriptContent()
    {
        var batch = LoadBatch();
        Conversation(batch, "1").Client = "<script>alert(1)</script>";
        var html = ReportBuilder.Build(batch);
        Assert.That(html, Does.Contain("&lt;script&gt;alert(1)&lt;/script&gt;"));
        Assert.That(html, Does.Not.Contain("<script>"));
    }

    private static ConversationResult Conversation(ResultBatch batch, string id) =>
        batch.Conversations.Single(x => x.TranscriptId == id);

    private static ResultBatch LoadBatch() =>
        JsonSerializer.Deserialize<ResultBatch>(File.ReadAllText(Path.Combine(Root(), "data", "demo-results.json")), Json)!;

    private static Dictionary<string, string> LoadTranscripts() =>
        Directory.GetFiles(Path.Combine(Root(), "data", "transcripts"), "*.txt")
            .ToDictionary(path => Path.GetFileNameWithoutExtension(path)!, File.ReadAllText);

    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "data", "demo-results.json"))) return directory.FullName;
        throw new DirectoryNotFoundException("Project root with demo data not found.");
    }
}
