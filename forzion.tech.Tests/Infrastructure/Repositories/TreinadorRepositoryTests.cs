using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace forzion.tech.Tests.Infrastructure.Repositories;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class TreinadorRepositoryTests(InfrastructureTestFixture fixture)
{
    // Regressão: treinador com vínculo→assinatura→pagamento deve ser excluível.
    // FKs RESTRICT (assinaturas→vínculo/pacote, pagamentos→assinatura, vínculo→pacote)
    // exigem ordem pagamentos→assinaturas→vínculos→pacotes; ordem errada estourava FK.
    [Fact]
    public async Task ExcluirComDependenciasAsync_ComAssinaturaEPagamento_ExcluiSemViolarFK()
    {
        Guid treinadorId, contaId, assinaturaId, pagamentoId, vinculoId, pacoteId;

        await using (var seedCtx = fixture.CreateContext())
        {
            var contaT = Conta.Criar(Email.Criar($"t{Guid.NewGuid():N}@test.com").Value, "hash", TipoConta.Treinador, DateTime.UtcNow).Value;
            var treinador = Treinador.Criar(contaT.Id, $"Tr{Guid.NewGuid():N}", DateTime.UtcNow).Value;
            var contaA = Conta.Criar(Email.Criar($"a{Guid.NewGuid():N}@test.com").Value, "hash", TipoConta.Aluno, DateTime.UtcNow).Value;
            var aluno = Aluno.Criar(contaA.Id, $"Al{Guid.NewGuid():N}", DateTime.UtcNow).Value;
            var pacote = Pacote.Criar(treinador.Id, $"Pac{Guid.NewGuid():N}", 99.90m, DateTime.UtcNow).Value;
            var vinculo = VinculoTreinadorAluno.Criar(treinador.Id, aluno.Id, DateTime.UtcNow).Value;
            vinculo.Aprovar(treinador.Id, pacote.Id, DateTime.UtcNow);
            var assinatura = AssinaturaAluno.Criar(vinculo.Id, pacote.Id, treinador.Id, aluno.Id, 99.90m, DateTime.UtcNow).Value;
            var pagamento = Pagamento.Criar(assinatura.Id, 99.90m, DateTime.UtcNow).Value;

            await seedCtx.Contas.AddRangeAsync(contaT, contaA);
            await seedCtx.Treinadores.AddAsync(treinador);
            await seedCtx.Alunos.AddAsync(aluno);
            await seedCtx.Pacotes.AddAsync(pacote);
            await seedCtx.VinculosTreinadorAluno.AddAsync(vinculo);
            await seedCtx.AssinaturaAlunos.AddAsync(assinatura);
            await seedCtx.Pagamentos.AddAsync(pagamento);
            await seedCtx.SaveChangesAsync();

            treinadorId = treinador.Id;
            contaId = contaT.Id;
            assinaturaId = assinatura.Id;
            pagamentoId = pagamento.Id;
            vinculoId = vinculo.Id;
            pacoteId = pacote.Id;
        }

        await using (var actCtx = fixture.CreateContext())
        {
            var treinador = await actCtx.Treinadores.FirstAsync(t => t.Id == treinadorId);

            var act = async () => await new TreinadorRepository(actCtx, TimeProvider.System).ExcluirComDependenciasAsync(treinador, Guid.NewGuid());

            await act.Should().NotThrowAsync();
        }

        await using (var assertCtx = fixture.CreateContext())
        {
            (await assertCtx.Treinadores.AnyAsync(t => t.Id == treinadorId)).Should().BeFalse();
            (await assertCtx.Contas.AnyAsync(c => c.Id == contaId)).Should().BeFalse();
            (await assertCtx.AssinaturaAlunos.AnyAsync(a => a.Id == assinaturaId)).Should().BeFalse();
            (await assertCtx.Pagamentos.AnyAsync(p => p.Id == pagamentoId)).Should().BeFalse();
            (await assertCtx.VinculosTreinadorAluno.AnyAsync(v => v.Id == vinculoId)).Should().BeFalse();
            (await assertCtx.Pacotes.AnyAsync(p => p.Id == pacoteId)).Should().BeFalse();
        }
    }

    // AUD-03: leads.treinador_id → treinadores é RESTRICT; sem apagar leads/convites antes,
    // a exclusão abortava com violação de FK.
    [Fact]
    public async Task ExcluirComDependenciasAsync_ComLeadELeadConvite_ExcluiSemViolarFK()
    {
        Guid treinadorId, leadId, conviteId;

        await using (var seedCtx = fixture.CreateContext())
        {
            var contaT = Conta.Criar(Email.Criar($"t{Guid.NewGuid():N}@test.com").Value, "hash", TipoConta.Treinador, DateTime.UtcNow).Value;
            var treinador = Treinador.Criar(contaT.Id, $"Tr{Guid.NewGuid():N}", DateTime.UtcNow).Value;
            var contato = ContatoLead.Criar(TipoContatoLead.Email, $"lead{Guid.NewGuid():N}@test.com").Value;
            var consentimento = ConsentimentoLead.Criar("Contato comercial", DateTime.UtcNow, DateTime.UtcNow).Value;
            var lead = Lead.Criar(treinador.Id, "Fulano", contato, null, consentimento, null, LeadSource.Agent, null, null, DateTime.UtcNow).Value;
            var convite = LeadConvite.Criar(lead.Id, treinador.Id, "hash-token", DateTime.UtcNow.AddDays(14), DateTime.UtcNow).Value;

            await seedCtx.Contas.AddAsync(contaT);
            await seedCtx.Treinadores.AddAsync(treinador);
            await seedCtx.Leads.AddAsync(lead);
            await seedCtx.LeadConvites.AddAsync(convite);
            await seedCtx.SaveChangesAsync();

            treinadorId = treinador.Id;
            leadId = lead.Id;
            conviteId = convite.Id;
        }

        await using (var actCtx = fixture.CreateContext())
        {
            var treinador = await actCtx.Treinadores.FirstAsync(t => t.Id == treinadorId);

            var act = async () => await new TreinadorRepository(actCtx, TimeProvider.System).ExcluirComDependenciasAsync(treinador, Guid.NewGuid());

            await act.Should().NotThrowAsync();
        }

        await using (var assertCtx = fixture.CreateContext())
        {
            (await assertCtx.Treinadores.AnyAsync(t => t.Id == treinadorId)).Should().BeFalse();
            (await assertCtx.Leads.AnyAsync(l => l.Id == leadId)).Should().BeFalse();
            (await assertCtx.LeadConvites.AnyAsync(c => c.Id == conviteId)).Should().BeFalse();
        }
    }

    // Edge case da spec: lead que já converteu em aluno (leads.aluno_id também é RESTRICT) —
    // apagar o lead não pode ser barrado por essa segunda FK nem tentar apagar o aluno.
    [Fact]
    public async Task ExcluirComDependenciasAsync_ComLeadJaConvertidoEmAluno_ExcluiSemViolarFK()
    {
        Guid treinadorId, leadId, alunoId;

        await using (var seedCtx = fixture.CreateContext())
        {
            var contaT = Conta.Criar(Email.Criar($"t{Guid.NewGuid():N}@test.com").Value, "hash", TipoConta.Treinador, DateTime.UtcNow).Value;
            var treinador = Treinador.Criar(contaT.Id, $"Tr{Guid.NewGuid():N}", DateTime.UtcNow).Value;
            var contaA = Conta.Criar(Email.Criar($"a{Guid.NewGuid():N}@test.com").Value, "hash", TipoConta.Aluno, DateTime.UtcNow).Value;
            var aluno = Aluno.Criar(contaA.Id, $"Al{Guid.NewGuid():N}", DateTime.UtcNow).Value;
            var contato = ContatoLead.Criar(TipoContatoLead.Email, $"lead{Guid.NewGuid():N}@test.com").Value;
            var consentimento = ConsentimentoLead.Criar("Contato comercial", DateTime.UtcNow, DateTime.UtcNow).Value;
            var lead = Lead.Criar(treinador.Id, "Fulano", contato, null, consentimento, null, LeadSource.Agent, null, null, DateTime.UtcNow).Value;
            lead.Converter(aluno.Id, DateTime.UtcNow);

            await seedCtx.Contas.AddRangeAsync(contaT, contaA);
            await seedCtx.Treinadores.AddAsync(treinador);
            await seedCtx.Alunos.AddAsync(aluno);
            await seedCtx.Leads.AddAsync(lead);
            await seedCtx.SaveChangesAsync();

            treinadorId = treinador.Id;
            leadId = lead.Id;
            alunoId = aluno.Id;
        }

        await using (var actCtx = fixture.CreateContext())
        {
            var treinador = await actCtx.Treinadores.FirstAsync(t => t.Id == treinadorId);

            var act = async () => await new TreinadorRepository(actCtx, TimeProvider.System).ExcluirComDependenciasAsync(treinador, Guid.NewGuid());

            await act.Should().NotThrowAsync();
        }

        await using (var assertCtx = fixture.CreateContext())
        {
            (await assertCtx.Treinadores.AnyAsync(t => t.Id == treinadorId)).Should().BeFalse();
            (await assertCtx.Leads.AnyAsync(l => l.Id == leadId)).Should().BeFalse();
            (await assertCtx.Alunos.AnyAsync(a => a.Id == alunoId)).Should().BeTrue(
                "o aluno convertido não é apagado pela exclusão do treinador — só o lead que referencia ele");
        }
    }

    // Timestamp do log de exclusão (prova de auditoria, §2 specification-coding) usa o
    // relógio do servidor via TimeProvider injetado, não DateTime.UtcNow.
    [Fact]
    public async Task ExcluirComDependenciasAsync_LogAuditoria_UsaTimestampDoTimeProvider()
    {
        var instante = new DateTimeOffset(2026, 6, 7, 9, 30, 0, TimeSpan.Zero);
        var time = new FakeTimeProvider(instante);
        Guid treinadorId, adminId = Guid.NewGuid();

        await using (var seedCtx = fixture.CreateContext())
        {
            var contaT = Conta.Criar(Email.Criar($"t{Guid.NewGuid():N}@test.com").Value, "hash", TipoConta.Treinador, DateTime.UtcNow).Value;
            var treinador = Treinador.Criar(contaT.Id, $"Tr{Guid.NewGuid():N}", DateTime.UtcNow).Value;

            await seedCtx.Contas.AddAsync(contaT);
            await seedCtx.Treinadores.AddAsync(treinador);
            await seedCtx.SaveChangesAsync();

            treinadorId = treinador.Id;
        }

        await using (var actCtx = fixture.CreateContext())
        {
            var treinador = await actCtx.Treinadores.FirstAsync(t => t.Id == treinadorId);
            await new TreinadorRepository(actCtx, time).ExcluirComDependenciasAsync(treinador, adminId);
        }

        await using (var assertCtx = fixture.CreateContext())
        {
            var log = await assertCtx.LogsAprovacao
                .FirstAsync(l => l.EntidadeId == treinadorId && l.TipoAcao == TipoAcaoAprovacao.ExclusaoTreinador);

            log.CreatedAt.Should().Be(instante.UtcDateTime);
        }
    }

    // AUD-43: reads da borda de agente não podem trackear — ObterPorIdAsync fica tracked de
    // propósito para os caminhos de escrita (ex.: ExcluirComDependenciasAsync acima).
    [Fact]
    public async Task ObterPorIdSemTrackingAsync_NaoRastreiaEntidadeRetornada()
    {
        await using var ctx = fixture.CreateContext();
        var conta = Conta.Criar(Email.Criar($"t{Guid.NewGuid():N}@test.com").Value, "hash", TipoConta.Treinador, DateTime.UtcNow).Value;
        var treinador = Treinador.Criar(conta.Id, $"Tr{Guid.NewGuid():N}", DateTime.UtcNow).Value;
        await ctx.Contas.AddAsync(conta);
        await ctx.Treinadores.AddAsync(treinador);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        await new TreinadorRepository(ctx, TimeProvider.System).ObterPorIdSemTrackingAsync(treinador.Id);

        ctx.ChangeTracker.Entries<Treinador>().Should().BeEmpty();
    }

    [Fact]
    public async Task ObterPorIdAsync_RastreiaEntidadeRetornada_ParaCaminhosDeEscrita()
    {
        await using var ctx = fixture.CreateContext();
        var conta = Conta.Criar(Email.Criar($"t{Guid.NewGuid():N}@test.com").Value, "hash", TipoConta.Treinador, DateTime.UtcNow).Value;
        var treinador = Treinador.Criar(conta.Id, $"Tr{Guid.NewGuid():N}", DateTime.UtcNow).Value;
        await ctx.Contas.AddAsync(conta);
        await ctx.Treinadores.AddAsync(treinador);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        await new TreinadorRepository(ctx, TimeProvider.System).ObterPorIdAsync(treinador.Id);

        ctx.ChangeTracker.Entries<Treinador>().Should().ContainSingle();
    }
}
