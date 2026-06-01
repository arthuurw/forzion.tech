using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

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

            var act = async () => await new TreinadorRepository(actCtx).ExcluirComDependenciasAsync(treinador, Guid.NewGuid());

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
}
