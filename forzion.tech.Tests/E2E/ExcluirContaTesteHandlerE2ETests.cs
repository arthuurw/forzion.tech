using FluentAssertions;
using forzion.tech.Application.UseCases.Admin.TestData;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace forzion.tech.Tests.E2E;

// A ordem de delete FK-restrita só é provável contra um Postgres real: um FK Restrict
// não previsto faria o ExecuteDelete estourar aqui, não num mock. Footprint do register:
// conta + aluno + vínculo + logAprovacao.
[Collection(E2ECollection.Name)]
[Trait("Category", "Integration")]
public class ExcluirContaTesteHandlerE2ETests(RealPipelineFixture fixture)
{
    [Fact]
    public async Task HardDelete_RemoveConta_Aluno_Vinculo_E_LogAprovacao()
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

            db.Contas.AddRange(treinadorConta, alunoConta);
            db.Treinadores.Add(treinador);
            db.Alunos.Add(aluno);
            db.VinculosTreinadorAluno.Add(vinculo);
            db.LogsAprovacao.Add(log);
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
        (await verifyDb.Contas.AsNoTracking().AnyAsync(c => c.Id == treinadorContaId)).Should().BeTrue();
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
