// F17b (Fase 4 test remediation) — FluentValidation tests diretos para os 7
// validators admin-side restantes (F17 cobriu 7 core; aqui completa o batch).
// Padrao: por rule, happy + boundary + violacao.
//
// Coberto neste arquivo:
//   - CriarGrupoMuscular, AtualizarGrupoMuscular
//   - CriarExercicio
//   - AtualizarPacote
//   - CriarPlanoPlataforma, AtualizarPlanoPlataforma
//   - AdicionarExercicio (Treinos)
//
// HealthReport ja coberto em AtualizarHealthReportConfigCommandValidatorTests
// (3 tests pre-existentes — ok). AtualizarPerfil/Login/etc cobertos em
// CoreValidatorsTests.

using FluentAssertions;
using forzion.tech.Application.UseCases.Admin.GruposMusculares.AtualizarGrupoMuscular;
using forzion.tech.Application.UseCases.Admin.GruposMusculares.CriarGrupoMuscular;
using forzion.tech.Application.UseCases.Exercicios.CriarExercicio;
using forzion.tech.Application.UseCases.Pacotes.AtualizarPacote;
using forzion.tech.Application.UseCases.Planos.AtualizarPlanoPlataforma;
using forzion.tech.Application.UseCases.Planos.CriarPlanoPlataforma;
using forzion.tech.Application.UseCases.Treinos.AdicionarExercicio;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Tests.Application.Validators;

public class CriarGrupoMuscularCommandValidatorTests
{
    private readonly CriarGrupoMuscularCommandValidator _validator = new();

    [Fact]
    public void Valido_QuandoNomeOk()
        => _validator.Validate(new CriarGrupoMuscularCommand("Peito")).IsValid.Should().BeTrue();

    [Fact]
    public void Invalido_QuandoNomeVazio()
        => _validator.Validate(new CriarGrupoMuscularCommand("")).IsValid.Should().BeFalse();

    [Fact]
    public void Invalido_QuandoNomeAcimaDe100Chars()
        => _validator.Validate(new CriarGrupoMuscularCommand(new string('a', 101))).IsValid.Should().BeFalse();
}

public class AtualizarGrupoMuscularCommandValidatorTests
{
    private readonly AtualizarGrupoMuscularCommandValidator _validator = new();

    private static AtualizarGrupoMuscularCommand Cmd(string nome) =>
        new(Guid.NewGuid(), nome);

    [Fact]
    public void Valido_QuandoNomeOk() => _validator.Validate(Cmd("Costas")).IsValid.Should().BeTrue();

    [Fact]
    public void Invalido_QuandoNomeVazio() => _validator.Validate(Cmd("")).IsValid.Should().BeFalse();

    [Fact]
    public void Invalido_QuandoNomeAcimaDe100Chars()
        => _validator.Validate(Cmd(new string('a', 101))).IsValid.Should().BeFalse();
}

public class CriarExercicioCommandValidatorTests
{
    private readonly CriarExercicioCommandValidator _validator = new();

    private static CriarExercicioCommand Cmd(
        string nome = "Supino",
        Guid? grupoId = null,
        string? descricao = null) =>
        new(Guid.NewGuid(), nome, grupoId ?? Guid.NewGuid(), descricao);

    [Fact]
    public void Valido_QuandoTudoOk() => _validator.Validate(Cmd()).IsValid.Should().BeTrue();

    [Fact]
    public void Invalido_QuandoNomeVazio() => _validator.Validate(Cmd(nome: "")).IsValid.Should().BeFalse();

    [Fact]
    public void Invalido_QuandoNomeAcimaDe100Chars()
        => _validator.Validate(Cmd(nome: new string('a', 101))).IsValid.Should().BeFalse();

    [Fact]
    public void Invalido_QuandoGrupoMuscularEmpty()
        => _validator.Validate(Cmd(grupoId: Guid.Empty)).IsValid.Should().BeFalse();

    [Fact]
    public void Invalido_QuandoDescricaoAcimaDe500Chars()
        => _validator.Validate(Cmd(descricao: new string('a', 501))).IsValid.Should().BeFalse();

    [Fact]
    public void Valido_QuandoDescricaoNull() => _validator.Validate(Cmd(descricao: null)).IsValid.Should().BeTrue();
}

public class AtualizarPacoteCommandValidatorTests
{
    private readonly AtualizarPacoteCommandValidator _validator = new();

    private static AtualizarPacoteCommand Cmd(string? nome = null, decimal? preco = null, string? desc = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), nome, preco, desc);

    [Fact]
    public void Valido_QuandoTudoNull_Patch() => _validator.Validate(Cmd()).IsValid.Should().BeTrue();

    [Fact]
    public void Valido_QuandoNomeOk() => _validator.Validate(Cmd(nome: "Pro")).IsValid.Should().BeTrue();

    [Fact]
    public void Invalido_QuandoNomeVazio() => _validator.Validate(Cmd(nome: "")).IsValid.Should().BeFalse();

    [Fact]
    public void Invalido_QuandoNomeAcimaDe100Chars()
        => _validator.Validate(Cmd(nome: new string('a', 101))).IsValid.Should().BeFalse();

    [Fact]
    public void Valido_QuandoPrecoZero() => _validator.Validate(Cmd(preco: 0m)).IsValid.Should().BeTrue();

    [Fact]
    public void Invalido_QuandoPrecoNegativo() => _validator.Validate(Cmd(preco: -1m)).IsValid.Should().BeFalse();

    [Fact]
    public void Invalido_QuandoDescricaoAcimaDe500Chars()
        => _validator.Validate(Cmd(desc: new string('a', 501))).IsValid.Should().BeFalse();
}

public class CriarPlanoPlataformaCommandValidatorTests
{
    private readonly CriarPlanoPlataformaCommandValidator _validator = new();

    private static CriarPlanoPlataformaCommand Cmd(
        string nome = "Pro",
        TierPlano tier = TierPlano.Pro,
        int maxAlunos = 50,
        decimal preco = 299m,
        string? descricao = null) => new(nome, tier, maxAlunos, preco, descricao);

    [Fact]
    public void Valido_QuandoTudoOk() => _validator.Validate(Cmd()).IsValid.Should().BeTrue();

    [Fact]
    public void Invalido_QuandoNomeVazio() => _validator.Validate(Cmd(nome: "")).IsValid.Should().BeFalse();

    [Fact]
    public void Invalido_QuandoNomeAcimaDe100Chars()
        => _validator.Validate(Cmd(nome: new string('a', 101))).IsValid.Should().BeFalse();

    [Fact]
    public void Invalido_QuandoMaxAlunosZero() => _validator.Validate(Cmd(maxAlunos: 0)).IsValid.Should().BeFalse();

    [Fact]
    public void Invalido_QuandoMaxAlunosNegativo() => _validator.Validate(Cmd(maxAlunos: -1)).IsValid.Should().BeFalse();

    [Fact]
    public void Valido_QuandoPrecoZero() => _validator.Validate(Cmd(preco: 0m)).IsValid.Should().BeTrue();

    [Fact]
    public void Invalido_QuandoPrecoNegativo() => _validator.Validate(Cmd(preco: -1m)).IsValid.Should().BeFalse();
}

public class AtualizarPlanoPlataformaCommandValidatorTests
{
    private readonly AtualizarPlanoPlataformaCommandValidator _validator = new();

    private static AtualizarPlanoPlataformaCommand Cmd(
        string? nome = null, TierPlano? tier = null, int? maxAlunos = null,
        decimal? preco = null, string? desc = null) =>
        new(Guid.NewGuid(), nome, tier, maxAlunos, preco, desc);

    [Fact]
    public void Valido_QuandoTudoNull_Patch() => _validator.Validate(Cmd()).IsValid.Should().BeTrue();

    [Fact]
    public void Valido_QuandoNomeOk() => _validator.Validate(Cmd(nome: "Plus")).IsValid.Should().BeTrue();

    [Fact]
    public void Invalido_QuandoNomeVazio() => _validator.Validate(Cmd(nome: "")).IsValid.Should().BeFalse();

    [Fact]
    public void Invalido_QuandoMaxAlunosZero() => _validator.Validate(Cmd(maxAlunos: 0)).IsValid.Should().BeFalse();

    [Fact]
    public void Valido_QuandoPrecoZero() => _validator.Validate(Cmd(preco: 0m)).IsValid.Should().BeTrue();

    [Fact]
    public void Invalido_QuandoPrecoNegativo() => _validator.Validate(Cmd(preco: -1m)).IsValid.Should().BeFalse();
}

public class AdicionarExercicioCommandValidatorTests
{
    private readonly AdicionarExercicioCommandValidator _validator = new();

    private static SerieConfigCommand Serie(
        int quantidade = 4,
        int repeticoesMin = 8,
        int? repeticoesMax = 12,
        decimal? carga = null,
        int? descanso = 60,
        string? descricao = null) =>
        new(quantidade, repeticoesMin, repeticoesMax, descricao, carga, descanso);

    private static AdicionarExercicioCommand Cmd(IReadOnlyList<SerieConfigCommand>? series = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), series ?? new[] { Serie() });

    [Fact]
    public void Valido_QuandoTudoOk() => _validator.Validate(Cmd()).IsValid.Should().BeTrue();

    [Fact]
    public void Invalido_QuandoExercicioIdEmpty()
    {
        var cmd = new AdicionarExercicioCommand(Guid.NewGuid(), Guid.Empty, new[] { Serie() });
        _validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Invalido_QuandoSeriesVazias() => _validator.Validate(Cmd(series: Array.Empty<SerieConfigCommand>())).IsValid.Should().BeFalse();

    [Fact]
    public void Invalido_QuandoQuantidadeZero()
        => _validator.Validate(Cmd(series: new[] { Serie(quantidade: 0) })).IsValid.Should().BeFalse();

    [Fact]
    public void Invalido_QuandoRepeticoesMinZero()
        => _validator.Validate(Cmd(series: new[] { Serie(repeticoesMin: 0) })).IsValid.Should().BeFalse();

    [Fact]
    public void Invalido_QuandoRepeticoesMaxMenorQueMin()
        => _validator.Validate(Cmd(series: new[] { Serie(repeticoesMin: 12, repeticoesMax: 8) })).IsValid.Should().BeFalse();

    [Fact]
    public void Valido_QuandoRepeticoesMaxNull()
        => _validator.Validate(Cmd(series: new[] { Serie(repeticoesMax: null) })).IsValid.Should().BeTrue();

    [Fact]
    public void Invalido_QuandoCargaNegativa()
        => _validator.Validate(Cmd(series: new[] { Serie(carga: -1m) })).IsValid.Should().BeFalse();

    [Fact]
    public void Invalido_QuandoDescansoNegativo()
        => _validator.Validate(Cmd(series: new[] { Serie(descanso: -1) })).IsValid.Should().BeFalse();

    [Fact]
    public void Invalido_QuandoDescricaoAcimaDe100Chars()
        => _validator.Validate(Cmd(series: new[] { Serie(descricao: new string('a', 101)) })).IsValid.Should().BeFalse();
}
