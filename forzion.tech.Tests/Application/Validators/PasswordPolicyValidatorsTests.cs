using FluentAssertions;
using FluentValidation.Results;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.UseCases.Alunos.RegistrarAluno;
using forzion.tech.Application.UseCases.Auth.RedefinirSenha;
using forzion.tech.Application.UseCases.Conta.AlterarSenha;
using forzion.tech.Application.UseCases.Treinadores.RegistrarTreinador;
using forzion.tech.Domain.Enums;
using forzion.tech.Tests.TestDoubles;

namespace forzion.tech.Tests.Application.Validators;

public class PasswordPolicyValidatorsTests
{
    private const string SenhaValida = "SenhaForte123";
    private const string Senha11 = "SenhaForte1";
    private const string SemMaiuscula = "senhaforte123";
    private const string TokenValido = "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8e9f0a1b2";

    public static TheoryData<string> Alvos => new() { "aluno", "treinador", "alterar", "redefinir" };

    [Theory]
    [MemberData(nameof(Alvos))]
    public async Task SenhaForte_NaoComprometida_Passa(string alvo) =>
        (await Validar(alvo, SenhaValida, comprometida: false)).IsValid.Should().BeTrue();

    [Theory]
    [MemberData(nameof(Alvos))]
    public async Task AbaixoDe12Caracteres_Rejeita(string alvo) =>
        (await Validar(alvo, Senha11, comprometida: false)).IsValid.Should().BeFalse();

    [Theory]
    [MemberData(nameof(Alvos))]
    public async Task SemComposicao_Rejeita(string alvo) =>
        (await Validar(alvo, SemMaiuscula, comprometida: false)).IsValid.Should().BeFalse();

    [Theory]
    [MemberData(nameof(Alvos))]
    public async Task SenhaComprometida_Rejeita(string alvo)
    {
        var resultado = await Validar(alvo, SenhaValida, comprometida: true);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.ErrorMessage.Contains("vazamento"));
    }

    [Theory]
    [MemberData(nameof(Alvos))]
    public async Task HibpIndisponivel_FailOpen_Passa(string alvo) =>
        (await Validar(alvo, SenhaValida, comprometida: false)).IsValid.Should().BeTrue();

    [Theory]
    [MemberData(nameof(Alvos))]
    public async Task SenhaInvalida_NaoConsultaHibp(string alvo) =>
        (await Validar(alvo, Senha11, new HibpQueFalhaSeChamado())).IsValid.Should().BeFalse();

    [Theory]
    [MemberData(nameof(Alvos))]
    public async Task SenhaNula_RejeitaSemConsultarHibp(string alvo) =>
        (await Validar(alvo, null!, new HibpQueFalhaSeChamado())).IsValid.Should().BeFalse();

    private static Task<ValidationResult> Validar(string alvo, string senha, bool comprometida) =>
        Validar(alvo, senha, new FakePwnedPasswordsService(comprometida));

    private static Task<ValidationResult> Validar(string alvo, string senha, IPwnedPasswordsService svc)
    {
        return alvo switch
        {
            "aluno" => new RegistrarAlunoCommandValidator(svc)
                .ValidateAsync(new RegistrarAlunoCommand("a@x.com", senha, "Joao", Guid.NewGuid(), Guid.NewGuid())),
            "treinador" => new RegistrarTreinadorCommandValidator(svc)
                .ValidateAsync(new RegistrarTreinadorCommand("t@x.com", senha, "Ana", Guid.NewGuid(), ModoPagamentoAluno.Plataforma)),
            "alterar" => new AlterarSenhaCommandValidator(svc)
                .ValidateAsync(new AlterarSenhaCommand("SenhaAtual123", senha)),
            "redefinir" => new RedefinirSenhaCommandValidator(svc)
                .ValidateAsync(new RedefinirSenhaCommand(TokenValido, senha)),
            _ => throw new ArgumentOutOfRangeException(nameof(alvo)),
        };
    }

    private sealed class HibpQueFalhaSeChamado : IPwnedPasswordsService
    {
        public Task<bool> EstaComprometidaAsync(string senha, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("HIBP não deve ser consultado para senha inválida.");
    }
}
