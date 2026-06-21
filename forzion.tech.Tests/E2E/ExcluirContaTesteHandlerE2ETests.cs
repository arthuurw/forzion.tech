using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Admin.TestData;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace forzion.tech.Tests.E2E;

[Collection(E2ECollection.Name)]
[Trait("Category", "Integration")]
public class ExcluirContaTesteHandlerE2ETests(RealPipelineFixture fixture)
{
    [Fact]
    public async Task HardDelete_RemoveSubtreeAlunoComFilhosFkRestrict_E_DadosMfa()
    {
        var agora = DateTime.UtcNow;
        Guid alunoContaId, alunoId, treinadorContaId;

        using (var seedScope = fixture.Services.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();

            var treinadorConta = Conta.Criar(Email.Criar($"t{Guid.NewGuid():N}@e2e.test").Value, "$2a$12$x", TipoConta.Treinador, agora).Value;
            var treinador = Treinador.Criar(treinadorConta.Id, "Treinador Teste", agora).Value;
            var alunoConta = Conta.Criar(Email.Criar($"a{Guid.NewGuid():N}@e2e.test").Value, "$2a$12$x", TipoConta.Aluno, agora).Value;
            var aluno = Aluno.Criar(alunoConta.Id, "Aluno Teste", agora).Value;
            var vinculo = VinculoTreinadorAluno.Criar(treinador.Id, aluno.Id, agora, null).Value;
            var log = LogAprovacao.Registrar(TipoAcaoAprovacao.AprovacaoVinculo, treinadorConta.Id, alunoConta.Id, "Aluno", agora).Value;

            var pacote = Pacote.Criar(treinador.Id, "Plano Teste", 100m, agora).Value;
            var treino = Treino.Criar("Treino Teste", ObjetivoTreino.Forca, treinador.Id, agora).Value;
            var treinoAluno = TreinoAluno.Criar(treino.Id, aluno.Id, agora).Value;
            var execucao = ExecucaoTreino.Criar(treino.Id, aluno.Id, agora, agora).Value;
            var assinatura = AssinaturaAluno.Criar(vinculo.Id, pacote.Id, treinador.Id, aluno.Id, 100m, agora).Value;
            var assinante = Assinante.Criar(aluno.Id, "Aluno Teste", null, agora);

            var contaMfa = ContaMfa.Criar(alunoConta.Id, "secret", agora).Value;
            var recovery = MfaRecoveryCode.Criar(alunoConta.Id, "hash", agora).Value;
            var trusted = TrustedDevice.Criar(alunoConta.Id, "tokenhash", agora.AddDays(30), agora).Value;
            var challenge = MfaChallenge.Criar(alunoConta.Id, "codehash", MfaProposito.LoginFallback, agora.AddMinutes(5), agora).Value;

            var passwordResetToken = PasswordResetToken.Criar(alunoConta.Id, "resethash", agora.AddHours(1), agora).Value;
            var emailVerificationToken = EmailVerificationToken.Criar(alunoConta.Id, "verifyhash", agora.AddHours(1), agora).Value;
            var trocaEmailToken = TrocaEmailToken.Criar(alunoConta.Id, $"novo{Guid.NewGuid():N}@e2e.test", "trocahash", agora.AddHours(1), agora).Value;

            db.Contas.AddRange(treinadorConta, alunoConta);
            db.Treinadores.Add(treinador);
            db.Alunos.Add(aluno);
            db.VinculosTreinadorAluno.Add(vinculo);
            db.LogsAprovacao.Add(log);
            db.Pacotes.Add(pacote);
            db.Treinos.Add(treino);
            db.TreinoAlunos.Add(treinoAluno);
            db.ExecucoesTreino.Add(execucao);
            db.AssinaturaAlunos.Add(assinatura);
            db.Assinantes.Add(assinante);
            db.ContasMfa.Add(contaMfa);
            db.MfaRecoveryCodes.Add(recovery);
            db.TrustedDevices.Add(trusted);
            db.MfaChallenges.Add(challenge);
            db.PasswordResetTokens.Add(passwordResetToken);
            db.EmailVerificationTokens.Add(emailVerificationToken);
            db.TrocaEmailTokens.Add(trocaEmailToken);
            await db.SaveChangesAsync();

            alunoContaId = alunoConta.Id;
            alunoId = aluno.Id;
            treinadorContaId = treinadorConta.Id;
        }

        using (var scope = fixture.Services.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<ExcluirContaTesteHandler>();
            var result = await handler.HandleAsync(new ExcluirContaTesteCommand(alunoContaId));
            result.IsSuccess.Should().BeTrue();
        }

        using var verifyScope = fixture.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await verifyDb.Contas.AsNoTracking().AnyAsync(c => c.Id == alunoContaId)).Should().BeFalse();
        (await verifyDb.Alunos.AsNoTracking().AnyAsync(a => a.ContaId == alunoContaId)).Should().BeFalse();
        (await verifyDb.VinculosTreinadorAluno.AsNoTracking().AnyAsync(v => v.AlunoId == alunoId)).Should().BeFalse();
        (await verifyDb.LogsAprovacao.AsNoTracking().AnyAsync(l => l.EntidadeId == alunoContaId)).Should().BeFalse();
        (await verifyDb.ExecucoesTreino.AsNoTracking().AnyAsync(e => e.AlunoId == alunoId)).Should().BeFalse();
        (await verifyDb.AssinaturaAlunos.AsNoTracking().AnyAsync(a => a.AlunoId == alunoId)).Should().BeFalse();
        (await verifyDb.TreinoAlunos.AsNoTracking().AnyAsync(ta => ta.AlunoId == alunoId)).Should().BeFalse();
        (await verifyDb.Assinantes.AsNoTracking().AnyAsync(a => a.AlunoId == alunoId)).Should().BeFalse();
        (await verifyDb.ContasMfa.AsNoTracking().AnyAsync(m => m.ContaId == alunoContaId)).Should().BeFalse();
        (await verifyDb.MfaRecoveryCodes.AsNoTracking().AnyAsync(m => m.ContaId == alunoContaId)).Should().BeFalse();
        (await verifyDb.TrustedDevices.AsNoTracking().AnyAsync(m => m.ContaId == alunoContaId)).Should().BeFalse();
        (await verifyDb.MfaChallenges.AsNoTracking().AnyAsync(m => m.ContaId == alunoContaId)).Should().BeFalse();
        (await verifyDb.PasswordResetTokens.AsNoTracking().AnyAsync(t => t.ContaId == alunoContaId)).Should().BeFalse();
        (await verifyDb.EmailVerificationTokens.AsNoTracking().AnyAsync(t => t.ContaId == alunoContaId)).Should().BeFalse();
        (await verifyDb.TrocaEmailTokens.AsNoTracking().AnyAsync(t => t.ContaId == alunoContaId)).Should().BeFalse();
        (await verifyDb.Contas.AsNoTracking().AnyAsync(c => c.Id == treinadorContaId)).Should().BeTrue();
    }

    [Fact]
    public async Task ExcluirLogPorContaId_RemovePorEntidade_PreservaLogOndeContaEhApenasAtor()
    {
        var agora = DateTime.UtcNow;
        var contaId = Guid.NewGuid();
        var outraEntidade = Guid.NewGuid();
        Guid idLogEntidade, idLogAtor;

        using (var seedScope = fixture.Services.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var logEntidade = LogAprovacao.Registrar(TipoAcaoAprovacao.AprovacaoVinculo, Guid.NewGuid(), contaId, "Conta", agora).Value;
            var logAtor = LogAprovacao.Registrar(TipoAcaoAprovacao.AprovacaoVinculo, contaId, outraEntidade, "Conta", agora).Value;
            idLogEntidade = logEntidade.Id;
            idLogAtor = logAtor.Id;
            db.LogsAprovacao.AddRange(logEntidade, logAtor);
            await db.SaveChangesAsync();
        }

        using (var scope = fixture.Services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ILogAprovacaoRepository>();
            await repo.ExcluirPorContaIdAsync(contaId);
        }

        using var verifyScope = fixture.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await verifyDb.LogsAprovacao.AsNoTracking().AnyAsync(l => l.Id == idLogEntidade)).Should().BeFalse();
        (await verifyDb.LogsAprovacao.AsNoTracking().AnyAsync(l => l.Id == idLogAtor)).Should().BeTrue();
    }

    [Fact]
    public async Task ContaNaoTeste_RecusaComValidation422()
    {
        var agora = DateTime.UtcNow;
        Guid contaId;
        using (var seedScope = fixture.Services.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var conta = Conta.Criar(Email.Criar($"real{Guid.NewGuid():N}@gmail.com").Value, "$2a$12$x", TipoConta.Aluno, agora).Value;
            contaId = conta.Id;
            db.Contas.Add(conta);
            await db.SaveChangesAsync();
        }

        using var scope = fixture.Services.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<ExcluirContaTesteHandler>();
        var result = await handler.HandleAsync(new ExcluirContaTesteCommand(contaId));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error!.Code.Should().Be("testdata.conta_nao_e_teste");

        using var verifyScope = fixture.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await verifyDb.Contas.AsNoTracking().AnyAsync(c => c.Id == contaId)).Should().BeTrue();
    }

    [Fact]
    public async Task ContaInexistente_RetornaNotFound()
    {
        using var scope = fixture.Services.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<ExcluirContaTesteHandler>();
        var result = await handler.HandleAsync(new ExcluirContaTesteCommand(Guid.NewGuid()));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error!.Code.Should().Be("conta.nao_encontrada");
    }

    [Fact]
    public async Task Listar_IncluiSomenteContasComDominioDeTeste()
    {
        var agora = DateTime.UtcNow;
        var emailTeste = $"lst{Guid.NewGuid():N}@e2e.test";
        var emailReal = $"lst{Guid.NewGuid():N}@gmail.com";
        using (var seedScope = fixture.Services.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Contas.Add(Conta.Criar(Email.Criar(emailTeste).Value, "$2a$12$x", TipoConta.Aluno, agora).Value);
            db.Contas.Add(Conta.Criar(Email.Criar(emailReal).Value, "$2a$12$x", TipoConta.Aluno, agora).Value);
            await db.SaveChangesAsync();
        }

        using var scope = fixture.Services.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<ListarContasTesteHandler>();
        var contas = await handler.HandleAsync();

        var emails = contas.Select(c => c.Email).ToList();
        emails.Should().Contain(emailTeste);
        emails.Should().NotContain(emailReal);
    }

    [Fact]
    public async Task ContaTreinadorTeste_RecusaSemEstourarFkDoTreinador()
    {
        var agora = DateTime.UtcNow;
        Guid treinadorContaId;
        using (var seedScope = fixture.Services.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var conta = Conta.Criar(Email.Criar($"t{Guid.NewGuid():N}@e2e.test").Value, "$2a$12$x", TipoConta.Treinador, agora).Value;
            var treinador = Treinador.Criar(conta.Id, "Treinador Teste", agora).Value;
            treinadorContaId = conta.Id;
            db.Contas.Add(conta);
            db.Treinadores.Add(treinador);
            await db.SaveChangesAsync();
        }

        using var scope = fixture.Services.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<ExcluirContaTesteHandler>();
        var result = await handler.HandleAsync(new ExcluirContaTesteCommand(treinadorContaId));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error!.Code.Should().Be("testdata.tipo_nao_suportado");

        using var verifyScope = fixture.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await verifyDb.Contas.AsNoTracking().AnyAsync(c => c.Id == treinadorContaId)).Should().BeTrue();
    }
}
