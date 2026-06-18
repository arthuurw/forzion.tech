using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Tests.Infrastructure.Persistence;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class NotaFiscalPersistenceIntegrationTests(InfrastructureTestFixture fixture)
{
    private static readonly DateTime Agora = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    private async Task<Guid> SeedTreinadorAsync(AppDbContext ctx)
    {
        var conta = Conta.Criar(Email.Criar($"t{Guid.NewGuid():N}@test.com").Value, "hash", TipoConta.Treinador, Agora).Value;
        var treinador = Treinador.Criar(conta.Id, "Carlos Personal", Agora).Value;
        await ctx.Contas.AddAsync(conta);
        await ctx.Treinadores.AddAsync(treinador);
        await ctx.SaveChangesAsync();
        return treinador.Id;
    }

    private async Task<Guid> SeedPagamentoAsync(AppDbContext ctx, Guid treinadorId)
    {
        var plano = PlanoPlataforma.Criar("Pro", TierPlano.Pro, 50, 99.90m, Agora).Value;
        var assinatura = AssinaturaTreinador.Criar(treinadorId, plano.Id, 99.90m, Agora).Value;
        var pagamento = PagamentoTreinador.Criar(treinadorId, assinatura.Id, 99.90m, FinalidadePagamentoTreinador.Renovacao, Agora).Value;
        await ctx.PlanosPlataforma.AddAsync(plano);
        await ctx.AssinaturasTreinador.AddAsync(assinatura);
        await ctx.PagamentosTreinador.AddAsync(pagamento);
        await ctx.SaveChangesAsync();
        return pagamento.Id;
    }

    [Fact]
    public async Task Treinador_ComDadosFiscais_PersisteOwnedVoEEndereco()
    {
        Guid treinadorId;
        await using (var ctx = fixture.CreateContext())
        {
            treinadorId = await SeedTreinadorAsync(ctx);
            var treinador = await ctx.Treinadores.FindAsync(treinadorId);
            var endereco = EnderecoFiscal.Criar("Rua das Flores", "100", "Centro", "3550308", "SP", "01001000", "Sala 2").Value;
            var dados = DadosFiscais.Criar(TipoDocumentoFiscal.Cnpj, "11222333000181", "Carlos Personal LTDA", endereco, "987654").Value;
            treinador!.DefinirDadosFiscais(dados, Agora);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var treinador = await ctx.Treinadores.FindAsync(treinadorId);
            treinador!.DadosFiscais.Should().NotBeNull();
            treinador.DadosFiscais!.TipoDocumento.Should().Be(TipoDocumentoFiscal.Cnpj);
            treinador.DadosFiscais.Documento.Should().Be("11222333000181");
            treinador.DadosFiscais.RazaoSocial.Should().Be("Carlos Personal LTDA");
            treinador.DadosFiscais.InscricaoMunicipal.Should().Be("987654");
            treinador.DadosFiscais.Endereco.Logradouro.Should().Be("Rua das Flores");
            treinador.DadosFiscais.Endereco.CodigoMunicipioIbge.Should().Be("3550308");
            treinador.DadosFiscais.Endereco.Uf.Should().Be("SP");
            treinador.DadosFiscais.Endereco.Cep.Should().Be("01001000");
            treinador.DadosFiscais.Endereco.Complemento.Should().Be("Sala 2");
        }
    }

    [Fact]
    public async Task Treinador_SemDadosFiscais_OwnedVoFicaNull()
    {
        Guid treinadorId;
        await using (var ctx = fixture.CreateContext())
            treinadorId = await SeedTreinadorAsync(ctx);

        await using (var ctx = fixture.CreateContext())
        {
            var treinador = await ctx.Treinadores.FindAsync(treinadorId);
            treinador!.DadosFiscais.Should().BeNull();
        }
    }

    [Fact]
    public async Task NotaFiscal_Assinatura_RoundTrip()
    {
        Guid notaId;
        await using (var ctx = fixture.CreateContext())
        {
            var treinadorId = await SeedTreinadorAsync(ctx);
            var pagamentoId = await SeedPagamentoAsync(ctx, treinadorId);
            var nota = NotaFiscal.CriarAssinatura(treinadorId, pagamentoId, 99.90m, Agora).Value;
            await ctx.NotasFiscais.AddAsync(nota);
            await ctx.SaveChangesAsync();
            notaId = nota.Id;
        }

        await using (var ctx = fixture.CreateContext())
        {
            var nota = await ctx.NotasFiscais.FindAsync(notaId);
            nota!.Tipo.Should().Be(TipoNotaFiscal.AssinaturaSaaS);
            nota.Status.Should().Be(NotaFiscalStatus.Pendente);
            nota.Valor.Should().Be(99.90m);
            nota.PagamentoTreinadorId.Should().NotBeNull();
            nota.NumeroDps.Should().Be($"AS-{nota.PagamentoTreinadorId}");
        }
    }

    [Fact]
    public async Task NotaFiscal_Comissao_RoundTrip()
    {
        var inicio = new DateOnly(2026, 5, 1);
        var fim = new DateOnly(2026, 5, 31);
        Guid notaId;
        await using (var ctx = fixture.CreateContext())
        {
            var treinadorId = await SeedTreinadorAsync(ctx);
            var nota = NotaFiscal.CriarComissao(treinadorId, inicio, fim, 42.50m, Agora).Value;
            await ctx.NotasFiscais.AddAsync(nota);
            await ctx.SaveChangesAsync();
            notaId = nota.Id;
        }

        await using (var ctx = fixture.CreateContext())
        {
            var nota = await ctx.NotasFiscais.FindAsync(notaId);
            nota!.Tipo.Should().Be(TipoNotaFiscal.ComissaoMarketplace);
            nota.CompetenciaInicio.Should().Be(inicio);
            nota.CompetenciaFim.Should().Be(fim);
            nota.PagamentoTreinadorId.Should().BeNull();
        }
    }

    [Fact]
    public async Task NotaFiscal_PagamentoDuplicado_UniqueBloqueia()
    {
        Guid treinadorId;
        Guid pagamentoId;
        await using (var ctx = fixture.CreateContext())
        {
            treinadorId = await SeedTreinadorAsync(ctx);
            pagamentoId = await SeedPagamentoAsync(ctx, treinadorId);
            await ctx.NotasFiscais.AddAsync(NotaFiscal.CriarAssinatura(treinadorId, pagamentoId, 99.90m, Agora).Value);
            await ctx.SaveChangesAsync();
        }

        await using var ctx2 = fixture.CreateContext();
        await ctx2.NotasFiscais.AddAsync(NotaFiscal.CriarAssinatura(treinadorId, pagamentoId, 99.90m, Agora).Value);

        var act = async () => await ctx2.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task NotaFiscal_ComissaoMesmaCompetencia_UniqueBloqueia()
    {
        var inicio = new DateOnly(2026, 5, 1);
        var fim = new DateOnly(2026, 5, 31);
        Guid treinadorId;
        await using (var ctx = fixture.CreateContext())
        {
            treinadorId = await SeedTreinadorAsync(ctx);
            await ctx.NotasFiscais.AddAsync(NotaFiscal.CriarComissao(treinadorId, inicio, fim, 42.50m, Agora).Value);
            await ctx.SaveChangesAsync();
        }

        await using var ctx2 = fixture.CreateContext();
        await ctx2.NotasFiscais.AddAsync(NotaFiscal.CriarComissao(treinadorId, inicio, fim, 50m, Agora).Value);

        var act = async () => await ctx2.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
