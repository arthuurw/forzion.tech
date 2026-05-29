using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Domain.Entities;

public class TreinoTests
{
    private static readonly Guid TreinadorId = Guid.NewGuid();

    private static Treino CriarTreino() =>
        Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, TreinadorId, TestData.Agora).Value;

    private static TreinoExercicio AdicionarComSerie(Treino t, Guid? exercicioId = null)
    {
        var ex = t.AdicionarExercicio(exercicioId ?? Guid.NewGuid(), TestData.Agora).Value;
        ex.AdicionarSerie(3, 10, 12, null, null, null);
        return ex;
    }

    // --- Criar ---

    [Fact]
    public void Criar_DadosValidos_RetornaTreino()
    {
        var t = CriarTreino();
        t.Nome.Should().Be("Treino A");
        t.Objetivo.Should().Be(ObjetivoTreino.Hipertrofia);
        t.TreinadorId.Should().Be(TreinadorId);
        t.Exercicios.Should().BeEmpty();
        t.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Criar_ComDificuldadeEDatas_AtribuiCampos()
    {
        var inicio = new DateOnly(2025, 1, 1);
        var fim = new DateOnly(2025, 3, 31);

        var t = Treino.Criar("Treino B", ObjetivoTreino.Forca, TreinadorId, TestData.Agora,
            DificuldadeTreino.Avancado, inicio, fim).Value;

        t.Dificuldade.Should().Be(DificuldadeTreino.Avancado);
        t.DataInicio.Should().Be(inicio);
        t.DataFim.Should().Be(fim);
    }

    [Fact]
    public void Criar_SemDificuldadeExplicita_UsaIniciante()
    {
        var t = CriarTreino();
        t.Dificuldade.Should().Be(DificuldadeTreino.Iniciante);
    }

    [Fact]
    public void Criar_DataFimAnteriorAoInicio_LancaDomainException()
    {
        var r = Treino.Criar("T", ObjetivoTreino.Hipertrofia, TreinadorId, TestData.Agora,
            dataInicio: new DateOnly(2025, 6, 1),
            dataFim: new DateOnly(2025, 5, 1));

        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Criar_DataInicioSemFim_Permitido()
    {
        var t = Treino.Criar("T", ObjetivoTreino.Hipertrofia, TreinadorId, TestData.Agora,
            dataInicio: new DateOnly(2025, 1, 1)).Value;
        t.DataInicio.Should().NotBeNull();
        t.DataFim.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_NomeVazio_LancaDomainException(string nome)
    {
        var r = Treino.Criar(nome, ObjetivoTreino.Hipertrofia, TreinadorId, TestData.Agora);
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Criar_NomeMuitoLongo_LancaDomainException()
    {
        var r = Treino.Criar(new string('a', 101), ObjetivoTreino.Hipertrofia, TreinadorId, TestData.Agora);
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Criar_TreinadorIdVazio_LancaDomainException()
    {
        var r = Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, Guid.Empty, TestData.Agora);
        r.IsFailure.Should().BeTrue();
    }

    // --- Atualizar ---

    [Fact]
    public void Atualizar_DadosValidos_AtualizaCampos()
    {
        var t = CriarTreino();
        t.Atualizar("Treino B", ObjetivoTreino.Forca, TestData.Agora);
        t.Nome.Should().Be("Treino B");
        t.Objetivo.Should().Be(ObjetivoTreino.Forca);
        t.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Atualizar_NomeVazio_LancaDomainException()
    {
        var t = CriarTreino();
        var r = t.Atualizar("", null, TestData.Agora);
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Atualizar_ApenasObjetivo_MantemNome()
    {
        var t = CriarTreino();
        t.Atualizar(null, ObjetivoTreino.Resistencia, TestData.Agora);
        t.Nome.Should().Be("Treino A");
        t.Objetivo.Should().Be(ObjetivoTreino.Resistencia);
    }

    [Fact]
    public void Atualizar_Dificuldade_AlteraDificuldade()
    {
        var t = CriarTreino();
        t.Atualizar(null, null, TestData.Agora, DificuldadeTreino.Avancado);
        t.Dificuldade.Should().Be(DificuldadeTreino.Avancado);
    }

    [Fact]
    public void Atualizar_Datas_AtribuiDatas()
    {
        var t = CriarTreino();
        var inicio = new DateOnly(2025, 1, 1);
        var fim = new DateOnly(2025, 6, 30);

        t.Atualizar(null, null, TestData.Agora, dataInicio: inicio, dataFim: fim);

        t.DataInicio.Should().Be(inicio);
        t.DataFim.Should().Be(fim);
    }

    [Fact]
    public void Atualizar_DataFimAnteriorAoInicio_LancaDomainException()
    {
        var t = CriarTreino();
        var r = t.Atualizar(null, null, TestData.Agora,
            dataInicio: new DateOnly(2025, 6, 1),
            dataFim: new DateOnly(2025, 5, 1));

        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Atualizar_LimparDatas_ZeraCampos()
    {
        var t = Treino.Criar("T", ObjetivoTreino.Hipertrofia, TreinadorId, TestData.Agora,
            dataInicio: new DateOnly(2025, 1, 1),
            dataFim: new DateOnly(2025, 12, 31)).Value;

        t.Atualizar(null, null, TestData.Agora, limparDataInicio: true, limparDataFim: true);

        t.DataInicio.Should().BeNull();
        t.DataFim.Should().BeNull();
    }

    // --- AdicionarExercicio / AdicionarSerie ---

    [Fact]
    public void AdicionarExercicio_DadosValidos_AdicionaOrdenado()
    {
        var t = CriarTreino();
        AdicionarComSerie(t);
        AdicionarComSerie(t);
        t.Exercicios.Should().HaveCount(2);
        t.Exercicios[0].Ordem.Should().Be(1);
        t.Exercicios[1].Ordem.Should().Be(2);
    }

    [Fact]
    public void AdicionarSerie_QuantidadeZero_LancaDomainException()
    {
        var t = CriarTreino();
        var ex = t.AdicionarExercicio(Guid.NewGuid(), TestData.Agora).Value;
        var r = ex.AdicionarSerie(0, 12, null, null, null, null);
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AdicionarSerie_RepeticoesMinZero_LancaDomainException()
    {
        var t = CriarTreino();
        var ex = t.AdicionarExercicio(Guid.NewGuid(), TestData.Agora).Value;
        var r = ex.AdicionarSerie(3, 0, null, null, null, null);
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AdicionarSerie_CargaNegativa_LancaDomainException()
    {
        var t = CriarTreino();
        var ex = t.AdicionarExercicio(Guid.NewGuid(), TestData.Agora).Value;
        var r = ex.AdicionarSerie(3, 10, null, null, -1m, null);
        r.IsFailure.Should().BeTrue();
    }

    // --- RemoverExercicio ---

    [Fact]
    public void RemoverExercicio_ExercicioExistente_RemoveEReordena()
    {
        var t = CriarTreino();
        AdicionarComSerie(t);
        AdicionarComSerie(t);
        AdicionarComSerie(t);

        t.RemoverExercicio(t.Exercicios[0].Id, TestData.Agora);

        t.Exercicios.Should().HaveCount(2);
        t.Exercicios[0].Ordem.Should().Be(1);
        t.Exercicios[1].Ordem.Should().Be(2);
    }

    [Fact]
    public void RemoverExercicio_IdInexistente_LancaDomainException()
    {
        var t = CriarTreino();
        var r = t.RemoverExercicio(Guid.NewGuid(), TestData.Agora);
        r.IsFailure.Should().BeTrue();
    }

    // --- Duplicar ---

    [Fact]
    public void Duplicar_CriaCopiaComExercicios()
    {
        var t = CriarTreino();
        var ex = t.AdicionarExercicio(Guid.NewGuid(), TestData.Agora).Value;
        ex.AdicionarSerie(3, 10, 12, "Trabalho", 10m, 60);

        var copia = t.Duplicar(TestData.Agora).Value;

        copia.Id.Should().NotBe(t.Id);
        copia.Nome.Should().Be("Treino A (cópia)");
        copia.Objetivo.Should().Be(t.Objetivo);
        copia.TreinadorId.Should().Be(t.TreinadorId);
        copia.Exercicios.Should().HaveCount(1);
        copia.Exercicios[0].Id.Should().NotBe(t.Exercicios[0].Id);
        copia.Exercicios[0].ExercicioId.Should().Be(t.Exercicios[0].ExercicioId);
        copia.Exercicios[0].Series.Should().HaveCount(1);
    }

    [Fact]
    public void Duplicar_SemExercicios_CriaCopiaSemExercicios()
    {
        var t = CriarTreino();
        var copia = t.Duplicar(TestData.Agora).Value;
        copia.Exercicios.Should().BeEmpty();
    }

    // --- DuplicarPara ---

    [Fact]
    public void DuplicarPara_TreinadorValido_CriaComNovoTreinadorId()
    {
        var t = CriarTreino();
        var ex = t.AdicionarExercicio(Guid.NewGuid(), TestData.Agora).Value;
        ex.AdicionarSerie(3, 10, 12, null, null, null);

        var novoTreinadorId = Guid.NewGuid();
        var copia = t.DuplicarPara(novoTreinadorId, TestData.Agora).Value;

        copia.Id.Should().NotBe(t.Id);
        copia.TreinadorId.Should().Be(novoTreinadorId);
        copia.Nome.Should().Be(t.Nome);
        copia.Exercicios.Should().HaveCount(1);
    }

    [Fact]
    public void DuplicarPara_TreinadorIdVazio_LancaDomainException()
    {
        var t = CriarTreino();
        var r = t.DuplicarPara(Guid.Empty, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O treinador de destino é inválido.");
    }

    // --- ValidarMutabilidade ---

    [Fact]
    public void ValidarMutabilidade_Executado_LancaTreinoExecutadoException()
    {
        var r = Treino.ValidarMutabilidade(true);
        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("treino.ja_executado");
        r.Error!.Message.Should().Be("Treino já executado não pode ser alterado.");
    }

    [Fact]
    public void ValidarMutabilidade_NaoExecutado_NaoLancaExcecao()
    {
        var r = Treino.ValidarMutabilidade(false);
        r.IsSuccess.Should().BeTrue();
    }

    // --- Atualizar — NomeMuitoLongo ---

    [Fact]
    public void Atualizar_NomeMuitoLongo_LancaDomainException()
    {
        var t = CriarTreino();
        var r = t.Atualizar(new string('a', 101), null, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O nome deve ter no máximo 100 caracteres.");
    }

    // --- TreinoExercicio.AtualizarObservacao ---

    [Fact]
    public void AtualizarObservacao_DadosValidos_AtualizaCampo()
    {
        var t = CriarTreino();
        var ex = t.AdicionarExercicio(Guid.NewGuid(), TestData.Agora).Value;

        ex.AtualizarObservacao("Manter postura");

        ex.Observacao.Should().Be("Manter postura");
    }

    [Fact]
    public void AtualizarObservacao_Null_ZeraCampo()
    {
        var t = CriarTreino();
        var ex = t.AdicionarExercicio(Guid.NewGuid(), TestData.Agora).Value;
        ex.AtualizarObservacao("Manter postura");

        ex.AtualizarObservacao(null);

        ex.Observacao.Should().BeNull();
    }

    [Fact]
    public void AtualizarObservacao_TextoBranco_ZeraCampo()
    {
        var t = CriarTreino();
        var ex = t.AdicionarExercicio(Guid.NewGuid(), TestData.Agora).Value;

        ex.AtualizarObservacao("   ");

        ex.Observacao.Should().BeNull();
    }

    [Fact]
    public void AtualizarObservacao_TextoMuitoLongo_LancaDomainException()
    {
        var t = CriarTreino();
        var ex = t.AdicionarExercicio(Guid.NewGuid(), TestData.Agora).Value;

        var r = ex.AtualizarObservacao(new string('a', 501));
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("A observação deve ter no máximo 500 caracteres.");
    }

    // --- TreinoExercicio.AtualizarSeries ---

    [Fact]
    public void AtualizarSeries_ListaVazia_LancaDomainException()
    {
        var t = CriarTreino();
        var ex = t.AdicionarExercicio(Guid.NewGuid(), TestData.Agora).Value;

        var r = ex.AtualizarSeries([]);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O exercício deve ter pelo menos um grupo de séries.");
    }

    [Fact]
    public void AtualizarSeries_ListaValida_SubstituiSeries()
    {
        var t = CriarTreino();
        var ex = t.AdicionarExercicio(Guid.NewGuid(), TestData.Agora).Value;
        ex.AdicionarSerie(3, 10, 12, null, null, null);

        ex.AtualizarSeries([(4, 8, 10, "Pesado", 20m, 90)]);

        ex.Series.Should().HaveCount(1);
        ex.Series[0].Quantidade.Should().Be(4);
        ex.Series[0].RepeticoesMin.Should().Be(8);
    }

    // --- SerieConfig guards (via AdicionarSerie) ---

    [Fact]
    public void AdicionarSerie_RepeticoesMaxMenorQueMin_LancaDomainException()
    {
        var t = CriarTreino();
        var ex = t.AdicionarExercicio(Guid.NewGuid(), TestData.Agora).Value;
        var r = ex.AdicionarSerie(3, 10, 8, null, null, null);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O máximo de repetições não pode ser menor que o mínimo.");
    }

    [Fact]
    public void AdicionarSerie_DescansoNegativo_LancaDomainException()
    {
        var t = CriarTreino();
        var ex = t.AdicionarExercicio(Guid.NewGuid(), TestData.Agora).Value;
        var r = ex.AdicionarSerie(3, 10, null, null, null, -1);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O descanso não pode ser negativo.");
    }

    [Fact]
    public void AdicionarSerie_DescricaoSoBrancos_SetaNull()
    {
        var t = CriarTreino();
        var ex = t.AdicionarExercicio(Guid.NewGuid(), TestData.Agora).Value;
        ex.AdicionarSerie(3, 10, null, "   ", null, null);

        ex.Series[0].Descricao.Should().BeNull();
    }
}
