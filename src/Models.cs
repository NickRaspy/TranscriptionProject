namespace TranscriptMvp;

public sealed class ResultBatch
{
    public required string Mode { get; set; }
    public required string GeneratedAt { get; set; }
    public required List<ConversationResult> Conversations { get; init; }
}

public sealed class ConversationResult
{
    public required string TranscriptId { get; set; }
    public required string ConversationDate { get; init; }
    public required string Client { get; set; }
    public required string Outcome { get; set; }
    public required AgreedAction NextStep { get; init; }
    public required List<AgreedAction> AdditionalAgreedActions { get; init; }
    public required List<string> ClientNeeds { get; init; }
    public required List<string> Risks { get; init; }
    public required List<string> ManagerMistakes { get; init; }
    public required List<string> ManagerAttention { get; init; }
    public required List<string> MissingOrAmbiguousInformation { get; set; }
    public required List<EvidenceItem> Evidence { get; init; }
}

public sealed class AgreedAction
{
    public required string Action { get; set; }
    public required string Responsible { get; set; }
    public string? Date { get; set; }
    public required string DateText { get; set; }
    public required bool DateIsAmbiguous { get; set; }
}

public sealed class EvidenceItem
{
    public required string Claim { get; set; }
    public required string Quote { get; set; }
}
