// F17 (Fase 3 test remediation) — FluentValidation tests diretos para os 7
// validators de fluxos core (auth + signup + perfil + treino + pacote).
//
// Padrao: por rule, testar happy + boundary + violacao. Os tests cobrem o
// validator ISOLADO (sem handler), garantindo que mudanca silenciosa de rule
// quebra um teste especifico em vez de quebrar um handler test rio abaixo.
//
// Os outros 8 validators (Admin/GruposMusculares, Admin/HealthReport,
// Exercicios, Pacotes/Atualizar, Planos/*, Treinos/AdicionarExercicio) ficam
// fora deste pass — admin-side e lower-priority. Backlog Fase 4.

using FluentAssertions;
using forzion.tech.Application.UseCases.Alunos.CadastrarAluno;
using forzion.tech.Application.UseCases.Alunos.RegistrarAluno;
using forzion.tech.Application.UseCases.Auth.Login;
using forzion.tech.Application.UseCases.Conta.AtualizarPerfil;
using forzion.tech.Application.UseCases.Pacotes.CriarPacote;
using forzion.tech.Application.UseCases.Treinadores.RegistrarTreinador;
using forzion.tech.Application.UseCases.Treinos.CriarTreino;
using forzion.tech.Domain.Enums;
using forzion.tech.Tests.TestDoubles;

namespace forzion.tech.Tests.Application.Validators;

public class CadastrarAlunoCommandValidatorTests
{
    private readonly CadastrarAlunoCommandValidator _validator = new();

    private static CadastrarAlunoCommand Cmd(string nome = "Joao", string? email = null, string? telefone = null) =>
        new(Guid.NewGuid(), nome, email, telefone);

    [Fact]
    public void Valido_QuandoNomeOk_EmailNull_TelefoneNull()
        => _validator.Validate(Cmd()).IsValid.Should().BeTrue();

    [Fact]
    public void Invalido_QuandoNomeVazio()
        => _validator.Validate(Cmd(nome: "")).IsValid.Should().BeFalse();

    [Fact]
    public void Invalido_QuandoNomeAcimaDe100Chars()
        => _validator.Validate(Cmd(nome: new string('a', 101))).IsValid.Should().BeFalse();

    [Fact]
    public void Invalido_QuandoEmailMalFormado()
        => _validator.Validate(Cmd(email: "nao-eh-email")).IsValid.Should().BeFalse();

    [Fact]
    public void Valido_QuandoEmailVazio_NaoAcionaEmailRule()
        => _validator.Validate(Cmd(email: "")).IsValid.Should().BeTrue();

    [Fact]
    public void Invalido_QuandoTelefoneAcimaDe20Chars()
        => _validator.Validate(Cmd(telefone: new string('9', 21))).IsValid.Should().BeFalse();
}

public class RegistrarAlunoCommandValidatorTests
{
    private readonly RegistrarAlunoCommandValidator _validator = new(new FakePwnedPasswordsService());

    private static RegistrarAlunoCommand Cmd(
        string email = "joao@x.com",
        string senha = "SenhaForte123",
        string nome = "Joao",
        int? diasDisponiveis = null,
        int? tempoMin = null) =>
        new(email, senha, nome, Guid.NewGuid(), Guid.NewGuid(),
            DiasDisponiveis: diasDisponiveis,
            TempoDisponivelMinutos: tempoMin);

    [Fact]
    public async Task Valido_QuandoTudoOk() => (await _validator.ValidateAsync(Cmd())).IsValid.Should().BeTrue();

    [Fact]
    public async Task Invalido_QuandoEmailVazio() => (await _validator.ValidateAsync(Cmd(email: ""))).IsValid.Should().BeFalse();

    [Fact]
    public async Task Invalido_QuandoSenhaMenorQue12() => (await _validator.ValidateAsync(Cmd(senha: "SenhaForte1"))).IsValid.Should().BeFalse();

    [Fact]
    public async Task Invalido_QuandoSenhaSemMinuscula() => (await _validator.ValidateAsync(Cmd(senha: "SENHAFORTE123"))).IsValid.Should().BeFalse();

    [Fact]
    public async Task Invalido_QuandoSenhaSemMaiuscula() => (await _validator.ValidateAsync(Cmd(senha: "senhaforte123"))).IsValid.Should().BeFalse();

    [Fact]
    public async Task Invalido_QuandoSenhaSemNumero() => (await _validator.ValidateAsync(Cmd(senha: "SenhaForteAbc"))).IsValid.Should().BeFalse();

    [Fact]
    public async Task Invalido_QuandoTreinadorIdEmpty()
    {
        var cmd = new RegistrarAlunoCommand("j@x.com", "SenhaForte123", "Joao", Guid.Empty, Guid.NewGuid());
        (await _validator.ValidateAsync(cmd)).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Invalido_QuandoPacoteIdEmpty()
    {
        var cmd = new RegistrarAlunoCommand("j@x.com", "SenhaForte123", "Joao", Guid.NewGuid(), Guid.Empty);
        (await _validator.ValidateAsync(cmd)).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Invalido_QuandoDiasDisponiveisZero() => (await _validator.ValidateAsync(Cmd(diasDisponiveis: 0))).IsValid.Should().BeFalse();

    [Fact]
    public async Task Invalido_QuandoDiasDisponiveisMaiorQue7() => (await _validator.ValidateAsync(Cmd(diasDisponiveis: 8))).IsValid.Should().BeFalse();

    [Fact]
    public async Task Valido_QuandoTempoMinutos60() => (await _validator.ValidateAsync(Cmd(tempoMin: 60))).IsValid.Should().BeTrue();

    [Fact]
    public async Task Invalido_QuandoTempoMinutosForaEnum()
    {
        // 999 nao e valor valido do TempoDisponivel enum.
        (await _validator.ValidateAsync(Cmd(tempoMin: 999))).IsValid.Should().BeFalse();
    }
}

public class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Fact]
    public void Valido_QuandoEmailEsenhaOk()
        => _validator.Validate(new LoginCommand("a@b.com", "12345678")).IsValid.Should().BeTrue();

    [Fact]
    public void Invalido_QuandoEmailVazio()
        => _validator.Validate(new LoginCommand("", "12345678")).IsValid.Should().BeFalse();

    [Fact]
    public void Invalido_QuandoEmailMalFormado()
        => _validator.Validate(new LoginCommand("notanemail", "12345678")).IsValid.Should().BeFalse();

    [Fact]
    public void Invalido_QuandoEmailAcimaDe256Chars()
    {
        // 251 + "@x.com" (6 chars) = 257, acima do limite 256.
        var longEmail = new string('a', 251) + "@x.com";
        _validator.Validate(new LoginCommand(longEmail, "12345678")).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Invalido_QuandoSenhaVazia()
        => _validator.Validate(new LoginCommand("a@b.com", "")).IsValid.Should().BeFalse();

    [Fact]
    public void Invalido_QuandoSenhaMenorQue8()
        => _validator.Validate(new LoginCommand("a@b.com", "1234567")).IsValid.Should().BeFalse();
}

public class RegistrarTreinadorCommandValidatorTests
{
    private readonly RegistrarTreinadorCommandValidator _validator = new(new FakePwnedPasswordsService());

    private static RegistrarTreinadorCommand Cmd(
        string email = "t@x.com",
        string senha = "SenhaForte123",
        string nome = "Coach",
        string? tel = null) => new(email, senha, nome, Guid.NewGuid(), forzion.tech.Domain.Enums.ModoPagamentoAluno.Plataforma, tel);

    [Fact]
    public async Task Valido_QuandoTudoOk() => (await _validator.ValidateAsync(Cmd())).IsValid.Should().BeTrue();

    [Fact]
    public async Task Invalido_QuandoEmailVazio() => (await _validator.ValidateAsync(Cmd(email: ""))).IsValid.Should().BeFalse();

    [Fact]
    public async Task Invalido_QuandoSenhaSemMaiuscula() => (await _validator.ValidateAsync(Cmd(senha: "senhaforte123"))).IsValid.Should().BeFalse();

    [Fact]
    public async Task Invalido_QuandoSenhaMenorQue12() => (await _validator.ValidateAsync(Cmd(senha: "SenhaForte1"))).IsValid.Should().BeFalse();

    [Fact]
    public async Task Invalido_QuandoSenhaAcimaDe72Chars()
        => (await _validator.ValidateAsync(Cmd(senha: "Sa1" + new string('x', 70)))).IsValid.Should().BeFalse();

    [Fact]
    public async Task Invalido_QuandoNomeAcimaDe100Chars()
        => (await _validator.ValidateAsync(Cmd(nome: new string('a', 101)))).IsValid.Should().BeFalse();

    [Fact]
    public async Task Invalido_QuandoTelefoneAcimaDe20Chars()
        => (await _validator.ValidateAsync(Cmd(tel: new string('9', 21)))).IsValid.Should().BeFalse();

    [Fact]
    public async Task Valido_QuandoTelefoneNull() => (await _validator.ValidateAsync(Cmd(tel: null))).IsValid.Should().BeTrue();
}

public class CriarTreinoCommandValidatorTests
{
    private readonly CriarTreinoCommandValidator _validator = new();

    private static CriarTreinoCommand Cmd(string nome = "Treino A", ObjetivoTreino obj = ObjetivoTreino.Hipertrofia) =>
        new(Guid.NewGuid(), null, nome, obj);

    [Fact]
    public void Valido_QuandoTudoOk() => _validator.Validate(Cmd()).IsValid.Should().BeTrue();

    [Fact]
    public void Invalido_QuandoNomeVazio() => _validator.Validate(Cmd(nome: "")).IsValid.Should().BeFalse();

    [Fact]
    public void Invalido_QuandoNomeAcimaDe100Chars()
        => _validator.Validate(Cmd(nome: new string('a', 101))).IsValid.Should().BeFalse();

    [Fact]
    public void Invalido_QuandoObjetivoForaEnum()
        => _validator.Validate(Cmd(obj: (ObjetivoTreino)999)).IsValid.Should().BeFalse();
}

public class CriarPacoteCommandValidatorTests
{
    private readonly CriarPacoteCommandValidator _validator = new();

    private static CriarPacoteCommand Cmd(
        string nome = "Basic",
        decimal preco = 100m,
        string? desc = null) => new(Guid.NewGuid(), nome, preco, desc);

    [Fact]
    public void Valido_QuandoTudoOk() => _validator.Validate(Cmd()).IsValid.Should().BeTrue();

    [Fact]
    public void Valido_QuandoPrecoZero() => _validator.Validate(Cmd(preco: 0m)).IsValid.Should().BeTrue();

    [Fact]
    public void Invalido_QuandoPrecoNegativo() => _validator.Validate(Cmd(preco: -1m)).IsValid.Should().BeFalse();

    [Fact]
    public void Invalido_QuandoNomeVazio() => _validator.Validate(Cmd(nome: "")).IsValid.Should().BeFalse();

    [Fact]
    public void Invalido_QuandoNomeAcimaDe100Chars()
        => _validator.Validate(Cmd(nome: new string('a', 101))).IsValid.Should().BeFalse();

    [Fact]
    public void Invalido_QuandoDescricaoAcimaDe500Chars()
        => _validator.Validate(Cmd(desc: new string('a', 501))).IsValid.Should().BeFalse();

    [Fact]
    public void Valido_QuandoDescricaoNull() => _validator.Validate(Cmd(desc: null)).IsValid.Should().BeTrue();
}

public class AtualizarPerfilCommandValidatorTests
{
    private readonly AtualizarPerfilCommandValidator _validator = new();

    [Fact]
    public void Valido_QuandoNomeOk()
        => _validator.Validate(new AtualizarPerfilCommand("Arthur")).IsValid.Should().BeTrue();

    [Fact]
    public void Invalido_QuandoNomeVazio()
        => _validator.Validate(new AtualizarPerfilCommand("")).IsValid.Should().BeFalse();

    [Fact]
    public void Invalido_QuandoNomeAcimaDe100Chars()
        => _validator.Validate(new AtualizarPerfilCommand(new string('a', 101))).IsValid.Should().BeFalse();
}

public class ValidacaoPtBrMessagesTests
{
    [Fact]
    public async Task RegistrarAluno_EmailVazio_MensagemPtBr_SemTermoIngles()
    {
        var validator = new RegistrarAlunoCommandValidator(new FakePwnedPasswordsService());
        var cmd = new RegistrarAlunoCommand("", "SenhaForte123", "Joao", Guid.NewGuid(), Guid.NewGuid());

        var msg = (await validator.ValidateAsync(cmd)).Errors.First(e => e.PropertyName == "Email").ErrorMessage;

        msg.Should().Be("O e-mail é obrigatório.");
        msg.Should().NotContainAny("must", "empty", "'Email'");
    }

    [Fact]
    public async Task RegistrarTreinador_NomeVazio_MensagemPtBr_SemTermoIngles()
    {
        var validator = new RegistrarTreinadorCommandValidator(new FakePwnedPasswordsService());
        var cmd = new RegistrarTreinadorCommand("t@x.com", "SenhaForte123", "", Guid.NewGuid(), ModoPagamentoAluno.Plataforma, null);

        var msg = (await validator.ValidateAsync(cmd)).Errors.Single(e => e.PropertyName == "Nome").ErrorMessage;

        msg.Should().Be("O nome é obrigatório.");
        msg.Should().NotContainAny("must", "empty");
    }
}
