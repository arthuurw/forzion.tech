using System.Collections.Concurrent;

namespace forzion.tech.AI.GuardRails;

public sealed class InMemoryDraftSuggestionService : IDraftSuggestionService
{
    private readonly ConcurrentDictionary<Guid, SugestaoDraft> _drafts = new();

    public Guid StoreDraft(SugestaoDraft draft)
    {
        var id = Guid.NewGuid();
        _drafts[id] = draft;
        PurgeExpired();
        return id;
    }

    public SugestaoDraft? GetDraft(Guid draftId, Guid treinadorId)
    {
        if (!_drafts.TryGetValue(draftId, out var draft)) return null;
        if (draft.TreinadorId != treinadorId) return null;
        if (draft.ExpiresAt < DateTime.UtcNow)
        {
            _drafts.TryRemove(draftId, out _);
            return null;
        }
        return draft;
    }

    public void RemoveDraft(Guid draftId) => _drafts.TryRemove(draftId, out _);

    private void PurgeExpired()
    {
        var now = DateTime.UtcNow;
        foreach (var key in _drafts.Keys)
            if (_drafts.TryGetValue(key, out var d) && d.ExpiresAt < now)
                _drafts.TryRemove(key, out _);
    }
}
