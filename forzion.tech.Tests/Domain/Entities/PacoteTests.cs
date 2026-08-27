using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Domain.Entities;

public class PacoteTests
{
    private static readonly Guid TreinadorId = Guid.NewGuid();

    // --- Criar ---

    [Fact]
    public void Criar_DadosValidos_RetornaPacote()
    {
        var p = Pacote.Criar(TreinadorId, "Pacote A", 99.90m, TestData.Agora, "Treino + whatsapp").Value;

        p.Id.Should().NotBeEmpty();
        p.TreinadorId.Should().Be(TreinadorId);
        p.Nome.Should().Be("Pacote A");
        p.Descricao.Should().Be("Treino + whatsapp");
        p.Preco.Should().Be(99.90m);
        p.IsAtivo.Should().BeTrue();
    }

    [Fact]
    public void Criar_SemDescricao_DescricaoNula()
    {
        var p = Pacote.Criar(TreinadorId, "Pacote A", 99.90m, TestData.Agora).Value;
        p.Descricao.Should().BeNull();
    }

    [Fact]
    public void Criar_TreinadorIdVazio_LancaDomainException()
    {
        var r = Pacote.Criar(Guid.Empty, "Nome", 0, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O identificador do treinador é inválido.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_NomeVazio_LancaDomainException(string nome)
    {
        var r = Pacote.Criar(TreinadorId, nome, 0, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O nome é obrigatório.");
    }

    [Fact]
    public void Criar_PrecoNegativo_LancaDomainException()
    {
        var r = Pacote.Criar(TreinadorId, "Nome", -1, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O preço não pode ser negativo.");
    }

    [Fact]
    public void Criar_DescricaoMuitoLonga_LancaDomainException()
    {
        var descricaoLonga = new string('x', 501);
        var r = Pacote.Criar(TreinadorId, "Nome", 0, TestData.Agora, descricaoLonga);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("A descrição deve ter no máximo 500 caracteres.");
    }

    // --- Atualizar ---

    [Fact]
    public void Atualizar_ComNome_AtualizaNome()
    {
        var p = Pacote.Criar(TreinadorId, "A", 0, TestData.Agora).Value;
        p.Atualizar("B", null, null, TestData.Agora);
        p.Nome.Should().Be("B");
        p.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Atualizar_ComDescricao_AtualizaDescricao()
    {
        var p = Pacote.Criar(TreinadorId, "A", 0, TestData.Agora).Value;
        p.Atualizar(null, null, "Premium com vídeo", TestData.Agora);
        p.Descricao.Should().Be("Premium com vídeo");
        p.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Atualizar_ComPreco_AtualizaPreco()
    {
        var p = Pacote.Criar(TreinadorId, "A", 50, TestData.Agora).Value;
        p.Atualizar(null, 120m, null, TestData.Agora);
        p.Preco.Should().Be(120m);
    }

    [Fact]
    public void Atualizar_CamposNulos_NaoAltera()
    {
        var p = Pacote.Criar(TreinadorId, "A", 50, TestData.Agora, "desc").Value;
        p.Atualizar(null, null, null, TestData.Agora);
        p.Nome.Should().Be("A");
        p.Descricao.Should().Be("desc");
        p.Preco.Should().Be(50);
    }

    [Fact]
    public void Atualizar_DescricaoMuitoLonga_LancaDomainException()
    {
        var p = Pacote.Criar(TreinadorId, "A", 0, TestData.Agora).Value;
        var descricaoLonga = new string('x', 501);
        var r = p.Atualizar(null, null, descricaoLonga, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("A descrição deve ter no máximo 500 caracteres.");
    }

    // --- Inativar ---

    [Fact]
    public void Inativar_Ativo_MudaParaInativo()
    {
        var p = Pacote.Criar(TreinadorId, "A", 0, TestData.Agora).Value;
        p.Inativar(TestData.Agora);
        p.IsAtivo.Should().BeFalse();
        p.UpdatedAt.Should().NotBeNull();
    }

    // --- TornarPublico ---

    [Fact]
    public void TornarPublico_SemCategoria_Falha()
    {
        var p = Pacote.Criar(TreinadorId, "A", 0, TestData.Agora).Value;

        var r = p.TornarPublico(TestData.Agora);

        r.IsFailure.Should().BeTrue();
        p.IsPublico.Should().BeFalse();
    }

    [Fact]
    public void TornarPublico_ComCategoriaEDuracao_Sucesso()
    {
        var p = Pacote.Criar(TreinadorId, "A", 0, TestData.Agora).Value;
        p.AtualizarCatalogoPublico("Pilates", 60, null, TestData.Agora);

        var r = p.TornarPublico(TestData.Agora);

        r.IsSuccess.Should().BeTrue();
        p.IsPublico.Should().BeTrue();
    }

    [Fact]
    public void TornarPublico_ComCategoriaSemDuracao_Falha()
    {
        var p = Pacote.Criar(TreinadorId, "A", 0, TestData.Agora).Value;
        p.AtualizarCatalogoPublico("Pilates", null, null, TestData.Agora);

        var r = p.TornarPublico(TestData.Agora);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("pacote.duracao_obrigatoria_para_publico");
        p.IsPublico.Should().BeFalse();
    }

    // --- TornarPrivado ---

    [Fact]
    public void TornarPrivado_NuncaFalha()
    {
        var p = Pacote.Criar(TreinadorId, "A", 0, TestData.Agora).Value;
        p.AtualizarCatalogoPublico("Pilates", 60, null, TestData.Agora);
        p.TornarPublico(TestData.Agora);

        p.TornarPrivado(TestData.Agora);

        p.IsPublico.Should().BeFalse();
    }

    // --- AtualizarCatalogoPublico ---

    [Fact]
    public void AtualizarCatalogoPublico_DadosValidos_AtualizaCampos()
    {
        var p = Pacote.Criar(TreinadorId, "A", 0, TestData.Agora).Value;

        var r = p.AtualizarCatalogoPublico("Pilates", 60, true, TestData.Agora);

        r.IsSuccess.Should().BeTrue();
        p.Categoria.Should().Be("Pilates");
        p.DuracaoMinutos.Should().Be(60);
        p.TrialDisponivel.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(481)]
    public void AtualizarCatalogoPublico_DuracaoInvalida_Falha(int duracao)
    {
        var p = Pacote.Criar(TreinadorId, "A", 0, TestData.Agora).Value;

        var r = p.AtualizarCatalogoPublico(null, duracao, null, TestData.Agora);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("pacote.duracao_minutos_invalida");
        p.DuracaoMinutos.Should().BeNull();
    }

    [Fact]
    public void AtualizarCatalogoPublico_DuracaoNoLimiteMaximo_Atualiza()
    {
        var p = Pacote.Criar(TreinadorId, "A", 0, TestData.Agora).Value;

        var r = p.AtualizarCatalogoPublico(null, 480, null, TestData.Agora);

        r.IsSuccess.Should().BeTrue();
        p.DuracaoMinutos.Should().Be(480);
    }

    [Fact]
    public void AtualizarCatalogoPublico_CamposNulos_NaoAltera()
    {
        var p = Pacote.Criar(TreinadorId, "A", 0, TestData.Agora).Value;
        p.AtualizarCatalogoPublico("Pilates", 60, true, TestData.Agora);

        p.AtualizarCatalogoPublico(null, null, null, TestData.Agora);

        p.Categoria.Should().Be("Pilates");
        p.DuracaoMinutos.Should().Be(60);
        p.TrialDisponivel.Should().BeTrue();
    }

    // --- CapacidadeMaxima ---

    [Fact]
    public void Criar_NasceComCapacidadeMaxima1()
    {
        var p = Pacote.Criar(TreinadorId, "A", 0, TestData.Agora).Value;
        p.CapacidadeMaxima.Should().Be(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AtualizarCatalogoPublico_CapacidadeMaximaInvalida_Falha(int capacidade)
    {
        var p = Pacote.Criar(TreinadorId, "A", 0, TestData.Agora).Value;

        var r = p.AtualizarCatalogoPublico(null, null, null, TestData.Agora, capacidade);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("pacote.capacidade_maxima_invalida");
        p.CapacidadeMaxima.Should().Be(1);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    public void AtualizarCatalogoPublico_CapacidadeMaximaValida_Atualiza(int capacidade)
    {
        var p = Pacote.Criar(TreinadorId, "A", 0, TestData.Agora).Value;

        var r = p.AtualizarCatalogoPublico(null, null, null, TestData.Agora, capacidade);

        r.IsSuccess.Should().BeTrue();
        p.CapacidadeMaxima.Should().Be(capacidade);
    }

    [Fact]
    public void AtualizarCatalogoPublico_CapacidadeMaximaNula_PreservaValorAnterior()
    {
        var p = Pacote.Criar(TreinadorId, "A", 0, TestData.Agora).Value;
        p.AtualizarCatalogoPublico(null, null, null, TestData.Agora, 5);

        var r = p.AtualizarCatalogoPublico(null, null, null, TestData.Agora, null);

        r.IsSuccess.Should().BeTrue();
        p.CapacidadeMaxima.Should().Be(5);
    }
}
