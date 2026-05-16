namespace forzion.tech.AI.GuardRails;

public interface IDraftSuggestionService
{
    Guid StoreDraft(SugestaoDraft draft);

    // Returns null if not found, ownership mismatch, or TTL expired.
    SugestaoDraft? GetDraft(Guid draftId, Guid treinadorId);

    void RemoveDraft(Guid draftId);
}
