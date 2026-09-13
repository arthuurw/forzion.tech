using FluentAssertions;
using forzion.tech.AI.GuardRails;

namespace forzion.tech.Tests.AI.GuardRails;

public class InMemoryDraftSuggestionServiceTests
{
    private readonly InMemoryDraftSuggestionService _sut = new();
    private readonly Guid _treinadorId = Guid.NewGuid();
    private readonly Guid _alunoId = Guid.NewGuid();

    private SugestaoDraft BuildDraft(TimeSpan? ttl = null) => new(
        TreinadorId: _treinadorId,
        AlunoId: _alunoId,
        Objetivo: "Hipertrofia",
        Dificuldade: "Intermediário",
        NumeroDeTreinos: 3,
        ExpiresAt: DateTime.UtcNow.Add(ttl ?? TimeSpan.FromMinutes(10)));

    [Fact]
    public void StoreDraft_ReturnsNewGuid()
    {
        var id = _sut.StoreDraft(BuildDraft());
        id.Should().NotBeEmpty();
    }

    [Fact]
    public void GetDraft_ValidOwnership_ReturnsDraft()
    {
        var id = _sut.StoreDraft(BuildDraft());
        var draft = _sut.GetDraft(id, _treinadorId);
        draft.Should().NotBeNull();
        draft!.Objetivo.Should().Be("Hipertrofia");
    }

    [Fact]
    public void GetDraft_WrongOwner_ReturnsNull()
    {
        var id = _sut.StoreDraft(BuildDraft());
        var draft = _sut.GetDraft(id, Guid.NewGuid());
        draft.Should().BeNull();
    }

    [Fact]
    public void GetDraft_UnknownId_ReturnsNull()
    {
        _sut.StoreDraft(BuildDraft());
        var draft = _sut.GetDraft(Guid.NewGuid(), _treinadorId);
        draft.Should().BeNull();
    }

    [Fact]
    public void GetDraft_ExpiredDraft_ReturnsNull()
    {
        var expired = BuildDraft(TimeSpan.FromSeconds(-1));
        var id = _sut.StoreDraft(expired);
        var draft = _sut.GetDraft(id, _treinadorId);
        draft.Should().BeNull();
    }

    [Fact]
    public void RemoveDraft_SubsequentGet_ReturnsNull()
    {
        var id = _sut.StoreDraft(BuildDraft());
        _sut.RemoveDraft(id);
        _sut.GetDraft(id, _treinadorId).Should().BeNull();
    }

    [Fact]
    public void RemoveDraft_UnknownId_DoesNotThrow()
    {
        var act = () => _sut.RemoveDraft(Guid.NewGuid());
        act.Should().NotThrow();
    }

    [Fact]
    public void StoreDraft_MultipleDrafts_AreIndependent()
    {
        var id1 = _sut.StoreDraft(BuildDraft());
        var id2 = _sut.StoreDraft(new SugestaoDraft(
            _treinadorId, _alunoId, "Forca", "Avançado", 4,
            DateTime.UtcNow.AddMinutes(10)));

        _sut.GetDraft(id1, _treinadorId)!.Objetivo.Should().Be("Hipertrofia");
        _sut.GetDraft(id2, _treinadorId)!.Objetivo.Should().Be("Forca");
    }
}
