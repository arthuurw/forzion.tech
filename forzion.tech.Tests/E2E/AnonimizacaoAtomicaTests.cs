using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Conta.Lgpd;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace forzion.tech.Tests.E2E;

// ATOM-01: prova de atomicidade contra Postgres real. Mock não serve aqui — o ponto é
// que o ExecuteUpdate (que roda direto no banco, fora do change tracker) participe da
// transação ambiente e seja revertido quando um passo posterior falha.
[Collection(E2ECollection.Name)]
[Trait("Category", "Integration")]
public class AnonimizacaoAtomicaTests(RealPipelineFixture fixture)
{
    [Fact]
    public async Task Anonimizar_FalhaNoLogAuditoria_RevertePIIDosDeliveryLogs()
    {
        var email = $"atom{Guid.NewGuid():N}@e2e.test";
        var resendId = $"resend_{Guid.NewGuid():N}";
        Guid contaId;
        string emailHash;

        // Seed: conta + um delivery log com o hash do e-mail. O scrub do log é ExecuteUpdate.
        using (var seedScope = fixture.Services.CreateScope())
        {
            var seedDb = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            emailHash = seedScope.ServiceProvider.GetRequiredService<IRecipientHasher>().Hash(email);
            var contaSeed = Conta.Criar(Email.Criar(email).Value, "$2a$12$x", TipoConta.Aluno, DateTime.UtcNow).Value;
            contaId = contaSeed.Id;
            seedDb.Contas.Add(contaSeed);
            seedDb.EmailDeliveryLogs.Add(
                EmailDeliveryLog.Criar(resendId, "delivered", emailHash, DateTime.UtcNow, DateTime.UtcNow));
            await seedDb.SaveChangesAsync();
        }

        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();

        // Repo de auditoria lança no último passo, depois do scrub do delivery log.
        var logFalho = new Mock<ILogAprovacaoRepository>();
        logFalho.Setup(r => r.AdicionarAsync(It.IsAny<LogAprovacao>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("falha simulada no log de auditoria"));

        // Repos reais sobre o MESMO AppDbContext do scope → mesma conexão/transação;
        // só o log de auditoria é trocado pelo mock que falha.
        var handler = new AnonimizarContaHandler(
            sp.GetRequiredService<IContaRepository>(),
            sp.GetRequiredService<IAlunoRepository>(),
            sp.GetRequiredService<ITreinadorRepository>(),
            sp.GetRequiredService<IVinculoTreinadorAlunoRepository>(),
            sp.GetRequiredService<IExecucaoTreinoRepository>(),
            sp.GetRequiredService<IAssinanteRepository>(),
            sp.GetRequiredService<IEmailDeliveryLogRepository>(),
            sp.GetRequiredService<IWhatsAppDeliveryLogRepository>(),
            sp.GetRequiredService<IMensagemSuporteRepository>(),
            logFalho.Object,
            sp.GetRequiredService<IPasswordHasher>(),
            db,
            db,
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<IUserContext>(),
            sp.GetRequiredService<ITokenRevogadoRepository>(),
            sp.GetRequiredService<IDatabaseErrorInspector>(),
            sp.GetRequiredService<IRefreshTokenFamilyRepository>());

        // Admin (RealizadoPorId != ContaId) dispensa verificação de senha.
        var act = async () => await handler.HandleAsync(new AnonimizarContaCommand(contaId, Guid.NewGuid()));
        await act.Should().ThrowAsync<InvalidOperationException>();

        // Contexto novo: lê o estado persistido, não o cache do scope da operação.
        using var verifyScope = fixture.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logPersistido = await verifyDb.EmailDeliveryLogs.AsNoTracking().FirstAsync(l => l.ResendMessageId == resendId);
        logPersistido.RecipientEmailHash.Should().Be(emailHash, "scrub via ExecuteUpdate deve ter sido revertido com a transação");
        var contaPersistida = await verifyDb.Contas.AsNoTracking().FirstAsync(c => c.Id == contaId);
        contaPersistida.AnonimizadaEm.Should().BeNull("anonimização é all-or-nothing");
    }
}
