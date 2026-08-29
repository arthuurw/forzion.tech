using FluentAssertions;
using forzion.tech.Application.UseCases.Agents.Agendamentos;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace forzion.tech.Tests.E2E;

[Collection(E2ECollection.Name)]
[Trait("Category", "Integration")]
public class ConcurrentLeadResolucaoRaceTests(RealPipelineFixture fixture)
{
    [Fact]
    public async Task ResolverAsyncEmParaleloParaOMesmoContatoNovo_ExatamenteUmLeadECriado()
    {
        var treinadorId = await SeedTreinadorAsync();
        var contato = ContatoLead.Criar(TipoContatoLead.Email, $"lead{Guid.NewGuid():N}@e2e.test").Value;
        var consentimento = ConsentimentoLead.Criar("Contato comercial", DateTime.UtcNow, DateTime.UtcNow).Value;
        var slotInicioUtc = DateTime.UtcNow.AddDays(3);

        using var startBarrier = new Barrier(participantCount: 2);

        Task<ResolucaoOutcome> ResolverAsync() => Task.Run(async () =>
        {
            startBarrier.SignalAndWait();
            using var scope = fixture.Services.CreateScope();
            var resolvedor = scope.ServiceProvider.GetRequiredService<ResolvedorLeadAgendamento>();
            try
            {
                var result = await resolvedor.ResolverAsync(
                    treinadorId, "Fulano", contato, consentimento, null, slotInicioUtc, DateTime.UtcNow);
                return new ResolucaoOutcome(result.IsSuccess, result.IsSuccess ? result.Value.Id : null, null);
            }
            catch (Exception ex)
            {
                return new ResolucaoOutcome(false, null, ex);
            }
        });

        var resultados = await Task.WhenAll(ResolverAsync(), ResolverAsync());

        resultados.Where(r => r.Excecao is not null).Should().BeEmpty(
            "nenhuma resolução concorrente de lead deve terminar em erro não tratado (500): {0}",
            string.Join(" || ", resultados.Where(r => r.Excecao is not null).Select(r => r.Excecao!.GetType().Name + ": " + r.Excecao!.Message)));

        resultados.Should().OnlyContain(r => r.Sucesso, "colisão de unicidade deve ser absorvida internamente, nunca propagar como falha");
        resultados.Select(r => r.LeadId).Distinct().Should().ContainSingle(
            "as duas resoluções concorrentes para o mesmo contato novo devem convergir para a MESMA ficha de lead");

        var quantidadeDeLeadsParaOContato = await ContarLeadsPorContatoAsync(treinadorId, contato.Valor);
        quantidadeDeLeadsParaOContato.Should().Be(1, "AUD-35: exatamente um lead deve ser criado, garantido pela UNIQUE parcial de banco");
    }

    private async Task<int> ContarLeadsPorContatoAsync(Guid treinadorId, string contatoValor)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Leads.CountAsync(l => l.TreinadorId == treinadorId && l.Contato.Valor == contatoValor);
    }

    private async Task<Guid> SeedTreinadorAsync()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var agora = DateTime.UtcNow;

        var email = Email.Criar($"t{Guid.NewGuid():N}@e2e.test").Value;
        var conta = Conta.Criar(email, "hash", TipoConta.Treinador, agora).Value;
        var treinador = Treinador.Criar(conta.Id, "Treinador Race", agora).Value;
        db.Contas.Add(conta);
        db.Treinadores.Add(treinador);

        await db.SaveChangesAsync();
        return treinador.Id;
    }

    private sealed record ResolucaoOutcome(bool Sucesso, Guid? LeadId, Exception? Excecao);
}
