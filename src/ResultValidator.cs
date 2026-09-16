using System.Globalization;
using System.Text.RegularExpressions;

namespace TranscriptMvp;

public static class ResultValidator
{
    public static void Validate(ResultBatch batch, Dictionary<string, string> transcripts)
    {
        Require(batch.Mode, "mode");
        if (!DateTimeOffset.TryParse(batch.GeneratedAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            throw new InvalidDataException("generatedAt must be ISO 8601.");
        if (batch.Conversations is null || batch.Conversations.Count != transcripts.Count)
            throw new InvalidDataException("Result count does not match input transcript count.");

        var seen = new HashSet<string>();
        foreach (var result in batch.Conversations)
        {
            Require(result.TranscriptId, "transcriptId");
            if (!seen.Add(result.TranscriptId) || !transcripts.TryGetValue(result.TranscriptId, out var source))
                throw new InvalidDataException($"Unknown or duplicate transcript ID: {result.TranscriptId}");
            if (!DateOnly.TryParseExact(result.ConversationDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                throw new InvalidDataException($"Invalid conversationDate in {result.TranscriptId}.");
            Require(result.Client, "client");
            Require(result.Outcome, "outcome");
            ValidateAction(result.NextStep, result.TranscriptId);
            foreach (var action in NotNull(result.AdditionalAgreedActions, "additionalAgreedActions"))
                ValidateAction(action, result.TranscriptId);
            ValidateList(result.ClientNeeds, "clientNeeds");
            ValidateList(result.Risks, "risks");
            ValidateList(result.ManagerMistakes, "managerMistakes");
            ValidateList(result.ManagerAttention, "managerAttention");
            ValidateList(result.MissingOrAmbiguousInformation, "missingOrAmbiguousInformation");
            if (result.Evidence is null || result.Evidence.Count == 0)
                throw new InvalidDataException($"No evidence for transcript {result.TranscriptId}.");
            var normalizedSource = Normalize(source);
            foreach (var item in result.Evidence)
            {
                Require(item.Claim, "evidence.claim");
                Require(item.Quote, "evidence.quote");
                if (!normalizedSource.Contains(Normalize(item.Quote), StringComparison.Ordinal))
                    throw new InvalidDataException($"Unverifiable quote in transcript {result.TranscriptId}: {item.Quote}");
            }
        }
    }

    private static void ValidateAction(AgreedAction action, string id)
    {
        if (action is null) throw new InvalidDataException($"Missing action in {id}.");
        Require(action.Action, "action");
        Require(action.Responsible, "responsible");
        Require(action.DateText, "dateText");
        if (action.DateIsAmbiguous && action.Date is not null)
            throw new InvalidDataException($"Ambiguous action has an exact date in {id}.");
        if (!action.DateIsAmbiguous && action.Date is null)
            throw new InvalidDataException($"Action in {id} has no exact date but is marked unambiguous.");
        if (action.Date is not null && !DateOnly.TryParseExact(action.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            && !DateTime.TryParseExact(action.Date, "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            && !DateTimeOffset.TryParseExact(action.Date, "yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            throw new InvalidDataException($"Action date is not ISO 8601 in {id}: {action.Date}");
        if (action.Date?.Contains('T') == true && action.Date.Length > 19
            && !action.Date.EndsWith("+05:00", StringComparison.Ordinal))
            throw new InvalidDataException($"Time must use Yekaterinburg UTC+05:00 in {id}.");
    }

    private static List<T> NotNull<T>(List<T>? list, string name) =>
        list ?? throw new InvalidDataException($"Missing {name}.");

    private static void ValidateList(List<string>? list, string name)
    {
        foreach (var value in NotNull(list, name)) Require(value, name);
    }

    private static void Require(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidDataException($"Required field is empty: {name}.");
    }

    private static string Normalize(string value) => Regex.Replace(value.Replace('ё', 'е').Replace('Ё', 'Е'), @"\s+", " ").Trim();
}
