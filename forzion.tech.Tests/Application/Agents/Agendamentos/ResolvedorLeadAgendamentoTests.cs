using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Agents.Agendamentos;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using Moq;

namespace forzion.tech.Tests.Application.Agents.Agendamentos;

public class ResolvedorLeadAgendamentoTests
{
    private readonly Mock<ILeadRepository> _leadRepo = new();
    private readonly ResolvedorLeadAgendamento _resolvedor;
    private static readonly DateTime Agora = new(2026, 8, 24, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SlotInicioUtc = new(2026, 8, 25, 11, 0, 0, DateTimeKind.Utc);

    public ResolvedorLeadAgendamentoTests()
    {
        _resolvedor = new ResolvedorLeadAgendamento(_leadRepo.Object);
    }

    private static Lead CriarLeadExistente(Guid treinadorId, ContatoLead contato, LeadStatus status = LeadStatus.Novo, bool anonimizado = false)
    {
        var consentimento = ConsentimentoLead.Criar("Contato comercial", Agora, Agora).Value;
        var lead = Lead.Criar(treinadorId, "Fulano", contato, null, consentimento, null, LeadSource.Agent, null, null, Agora).Value;
        switch (status)
        {
            case LeadStatus.EmContato:
                lead.MarcarEmContato(Guid.NewGuid(), null, Agora);
                break;
            case LeadStatus.Convertido:
                lead.Converter(Guid.NewGuid(), Agora);
                break;
            case LeadStatus.Descartado:
                lead.Descartar(MotivoDescarteLead.SemInteresse, Guid.NewGuid(), null, Agora);
                break;
        }
        if (anonimizado)
            lead.Anonimizar(Agora);
        return lead;
    }

    // --- AGF4-15 / D-I: dois contatos com grafia diferente e mesma normalização resolvem para o mesmo lead ---

    [Theory]
    [InlineData("  FULANO@LEAD.COM  ")]
    [InlineData("fulano@lead.com")]
    [InlineData("Fulano@Lead.com")]
    public async Task ResolverAsync_ContatosComGrafiasDiferentesENormalizacaoIgual_ResolvemParaOMesmoLeadExistente(string contatoBruto)
    {
        var treinadorId = Guid.NewGuid();
        var contatoNormalizado = ContatoLead.Criar(TipoContatoLead.Email, contatoBruto).Value;
        var leadExistente = CriarLeadExistente(treinadorId, contatoNormalizado);
        _leadRepo.Setup(r => r.ObterReutilizavelPorContatoAsync(treinadorId, "fulano@lead.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(leadExistente);
        var consentimento = ConsentimentoLead.Criar("Contato comercial", Agora, Agora).Value;

        var result = await _resolvedor.ResolverAsync(treinadorId, "Fulano", contatoNormalizado, consentimento, null, SlotInicioUtc, Agora);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(leadExistente.Id);
        _leadRepo.Verify(r => r.ObterReutilizavelPorContatoAsync(treinadorId, "fulano@lead.com", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolverAsync_LeadExistente_RegistraInteracaoEAtualizaUltimoToque()
    {
        var treinadorId = Guid.NewGuid();
        var contato = ContatoLead.Criar(TipoContatoLead.Email, "fulano@lead.com").Value;
        var leadExistente = CriarLeadExistente(treinadorId, contato);
        var ultimoToqueAntes = leadExistente.UltimoToqueEm;
        _leadRepo.Setup(r => r.ObterReutilizavelPorContatoAsync(treinadorId, contato.Valor, It.IsAny<CancellationToken>()))
            .ReturnsAsync(leadExistente);
        var consentimento = ConsentimentoLead.Criar("Contato comercial", Agora, Agora).Value;
        var agoraDaSolicitacao = Agora.AddDays(1);

        var result = await _resolvedor.ResolverAsync(treinadorId, "Fulano", contato, consentimento, null, SlotInicioUtc, agoraDaSolicitacao);

        result.IsSuccess.Should().BeTrue();
        result.Value.UltimoToqueEm.Should().Be(agoraDaSolicitacao).And.NotBe(ultimoToqueAntes);
        result.Value.Interacoes.Should().ContainSingle();
    }

    // --- lead Convertido/Descartado/anonimizado não é reusado: o filtro de status/anonimização é
    // aplicado dentro de ObterReutilizavelPorContatoAsync (Infrastructure) — pela fronteira do
    // resolvedor, um lead nesses estados é indistinguível de "nenhum lead encontrado" (o
    // repositório já devolve null). O resolvedor precisa reagir criando um lead novo em vez de
    // "reusar" undefined — é essa reação que este teste prova. ---

    [Theory]
    [InlineData(LeadStatus.Convertido, false)]
    [InlineData(LeadStatus.Descartado, false)]
    [InlineData(LeadStatus.Novo, true)]
    public async Task ResolverAsync_NenhumLeadReutilizavelEncontrado_CriaLeadNovoComSourceAgent(LeadStatus statusDoLeadExcluidoPeloRepo, bool anonimizadoPeloRepo)
    {
        var treinadorId = Guid.NewGuid();
        var contato = ContatoLead.Criar(TipoContatoLead.Email, "novo@lead.com").Value;
        var leadExcluido = CriarLeadExistente(treinadorId, contato, statusDoLeadExcluidoPeloRepo, anonimizadoPeloRepo);
        leadExcluido.Should().NotBeNull("documenta o cenário real: existe um lead com este contato, mas fora do critério de reuso");
        _leadRepo.Setup(r => r.ObterReutilizavelPorContatoAsync(treinadorId, contato.Valor, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lead?)null);
        var consentimento = ConsentimentoLead.Criar("Contato comercial", Agora, Agora).Value;

        var result = await _resolvedor.ResolverAsync(treinadorId, "Fulano Novo", contato, consentimento, null, SlotInicioUtc, Agora);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBe(leadExcluido.Id, "o lead excluído nunca pode ser reusado");
        result.Value.Source.Should().Be(LeadSource.Agent);
        result.Value.Status.Should().Be(LeadStatus.Novo);
        result.Value.TreinadorId.Should().Be(treinadorId);
        _leadRepo.Verify(r => r.AdicionarAsync(result.Value, It.IsAny<CancellationToken>()), Times.Once,
            "lead novo precisa ser staged pelo resolvedor — o handler não chama AdicionarAsync de novo");
    }

    [Fact]
    public async Task ResolverAsync_LeadExistente_NaoChamaAdicionarAsync()
    {
        var treinadorId = Guid.NewGuid();
        var contato = ContatoLead.Criar(TipoContatoLead.Email, "fulano@lead.com").Value;
        var leadExistente = CriarLeadExistente(treinadorId, contato);
        _leadRepo.Setup(r => r.ObterReutilizavelPorContatoAsync(treinadorId, contato.Valor, It.IsAny<CancellationToken>()))
            .ReturnsAsync(leadExistente);
        var consentimento = ConsentimentoLead.Criar("Contato comercial", Agora, Agora).Value;

        await _resolvedor.ResolverAsync(treinadorId, "Fulano", contato, consentimento, null, SlotInicioUtc, Agora);

        _leadRepo.Verify(r => r.AdicionarAsync(It.IsAny<Lead>(), It.IsAny<CancellationToken>()), Times.Never,
            "o lead reusado já está tracked pelo DbContext — chamar AdicionarAsync de novo duplicaria o insert");
    }
}
