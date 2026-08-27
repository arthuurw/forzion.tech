using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Services;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Domain.Entities;

public class PerfilPublicoTests
{
    private static EnderecoPublico Endereco() =>
        EnderecoPublico.Criar("Rua das Flores", "Centro", "São Paulo", "SP", "01001-000").Value;

    // --- CriarVazio ---

    [Fact]
    public void CriarVazio_RetornaNaoPublicadoSemHorarios()
    {
        var perfil = PerfilPublico.CriarVazio();

        perfil.IsPublicado.Should().BeFalse();
        perfil.HorariosFuncionamento.Should().BeEmpty();
        perfil.NomeFantasia.Should().BeNull();
        perfil.Endereco.Should().BeNull();
        perfil.Politicas.Should().BeNull();
    }

    // --- AtualizarDados ---

    [Fact]
    public void AtualizarDados_DadosValidos_AtualizaCampos()
    {
        var perfil = PerfilPublico.CriarVazio();
        var politicas = new Dictionary<string, string> { ["cancelamento"] = "24h de antecedência" };

        var r = perfil.AtualizarDados("Academia X", Endereco(), politicas, TestData.Agora);

        r.IsSuccess.Should().BeTrue();
        perfil.NomeFantasia.Should().Be("Academia X");
        perfil.Endereco.Should().NotBeNull();
        perfil.Politicas.Should().BeEquivalentTo(politicas);
        perfil.UpdatedAt.Should().Be(TestData.Agora);
    }

    // --- Publicar ---

    [Fact]
    public void Publicar_SemNomeFantasia_Falha()
    {
        var perfil = PerfilPublico.CriarVazio();

        var r = perfil.Publicar(TestData.Agora);

        r.IsFailure.Should().BeTrue();
        perfil.IsPublicado.Should().BeFalse();
    }

    [Fact]
    public void Publicar_ComNomeFantasia_Sucesso()
    {
        var perfil = PerfilPublico.CriarVazio();
        perfil.AtualizarDados("Academia X", null, null, TestData.Agora);

        var r = perfil.Publicar(TestData.Agora);

        r.IsSuccess.Should().BeTrue();
        perfil.IsPublicado.Should().BeTrue();
    }

    // --- Despublicar ---

    [Fact]
    public void Despublicar_NuncaFalhaEPreservaDados()
    {
        var perfil = PerfilPublico.CriarVazio();
        perfil.AtualizarDados("Academia X", Endereco(), null, TestData.Agora);
        perfil.AdicionarHorario(1, new TimeOnly(8, 0), new TimeOnly(12, 0), TestData.Agora);
        perfil.Publicar(TestData.Agora);

        perfil.Despublicar(TestData.Agora);

        perfil.IsPublicado.Should().BeFalse();
        perfil.NomeFantasia.Should().Be("Academia X");
        perfil.Endereco.Should().NotBeNull();
        perfil.HorariosFuncionamento.Should().ContainSingle();
    }

    [Fact]
    public void Despublicar_SemNuncaTerSidoPublicado_NaoFalha()
    {
        var perfil = PerfilPublico.CriarVazio();

        perfil.Despublicar(TestData.Agora);

        perfil.IsPublicado.Should().BeFalse();
    }

    // --- AdicionarHorario / RemoverHorario ---

    [Fact]
    public void AdicionarHorario_DadosValidos_Adiciona()
    {
        var perfil = PerfilPublico.CriarVazio();

        var r = perfil.AdicionarHorario(1, new TimeOnly(8, 0), new TimeOnly(12, 0), TestData.Agora);

        r.IsSuccess.Should().BeTrue();
        perfil.HorariosFuncionamento.Should().ContainSingle();
    }

    [Fact]
    public void AdicionarHorario_IntervaloInvalido_Falha()
    {
        var perfil = PerfilPublico.CriarVazio();

        var r = perfil.AdicionarHorario(1, new TimeOnly(12, 0), new TimeOnly(8, 0), TestData.Agora);

        r.IsFailure.Should().BeTrue();
        perfil.HorariosFuncionamento.Should().BeEmpty();
    }

    [Fact]
    public void RemoverHorario_Existente_Remove()
    {
        var perfil = PerfilPublico.CriarVazio();
        perfil.AdicionarHorario(1, new TimeOnly(8, 0), new TimeOnly(12, 0), TestData.Agora);
        var horarioId = perfil.HorariosFuncionamento[0].Id;

        var r = perfil.RemoverHorario(horarioId, TestData.Agora);

        r.IsSuccess.Should().BeTrue();
        perfil.HorariosFuncionamento.Should().BeEmpty();
    }

    [Fact]
    public void RemoverHorario_Inexistente_Falha()
    {
        var perfil = PerfilPublico.CriarVazio();

        var r = perfil.RemoverHorario(Guid.NewGuid(), TestData.Agora);

        r.IsFailure.Should().BeTrue();
    }

    // --- SubstituirHorarios ---

    [Fact]
    public void SubstituirHorarios_ListaValida_TrocaTodosOsHorarios()
    {
        var perfil = PerfilPublico.CriarVazio();
        perfil.AdicionarHorario(1, new TimeOnly(8, 0), new TimeOnly(12, 0), TestData.Agora);

        var r = perfil.SubstituirHorarios(
            [(2, new TimeOnly(9, 0), new TimeOnly(18, 0)), (2, new TimeOnly(19, 0), new TimeOnly(22, 0))],
            TestData.Agora);

        r.IsSuccess.Should().BeTrue();
        perfil.HorariosFuncionamento.Should().HaveCount(2);
        perfil.HorariosFuncionamento.Should().OnlyContain(h => h.DiaSemana == 2);
    }

    [Fact]
    public void SubstituirHorarios_ListaVazia_LimpaHorariosExistentes()
    {
        var perfil = PerfilPublico.CriarVazio();
        perfil.AdicionarHorario(1, new TimeOnly(8, 0), new TimeOnly(12, 0), TestData.Agora);

        var r = perfil.SubstituirHorarios([], TestData.Agora);

        r.IsSuccess.Should().BeTrue();
        perfil.HorariosFuncionamento.Should().BeEmpty();
    }

    [Fact]
    public void SubstituirHorarios_UmItemInvalido_FalhaEPreservaHorariosAntigos()
    {
        var perfil = PerfilPublico.CriarVazio();
        perfil.AdicionarHorario(1, new TimeOnly(8, 0), new TimeOnly(12, 0), TestData.Agora);

        var r = perfil.SubstituirHorarios(
            [(2, new TimeOnly(9, 0), new TimeOnly(18, 0)), (3, new TimeOnly(20, 0), new TimeOnly(10, 0))],
            TestData.Agora);

        r.IsFailure.Should().BeTrue();
        perfil.HorariosFuncionamento.Should().ContainSingle(h => h.DiaSemana == 1);
    }

    [Fact]
    public void SubstituirHorarios_ListaAcimaDoLimite_FalhaEPreservaHorariosAntigos()
    {
        var perfil = PerfilPublico.CriarVazio();
        perfil.AdicionarHorario(1, new TimeOnly(8, 0), new TimeOnly(12, 0), TestData.Agora);
        var acimaDoLimite = Enumerable.Repeat((2, new TimeOnly(9, 0), new TimeOnly(10, 0)), ParametrosDerivacao.MaxHorariosFuncionamento + 1).ToList();

        var r = perfil.SubstituirHorarios(acimaDoLimite, TestData.Agora);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("perfil_publico.horarios_excedem_limite");
        perfil.HorariosFuncionamento.Should().ContainSingle(h => h.DiaSemana == 1);
    }

    [Fact]
    public void SubstituirHorarios_ListaNoLimiteExato_Sucesso()
    {
        var perfil = PerfilPublico.CriarVazio();
        var noLimite = Enumerable.Repeat((2, new TimeOnly(9, 0), new TimeOnly(10, 0)), ParametrosDerivacao.MaxHorariosFuncionamento).ToList();

        var r = perfil.SubstituirHorarios(noLimite, TestData.Agora);

        r.IsSuccess.Should().BeTrue();
        perfil.HorariosFuncionamento.Should().HaveCount(ParametrosDerivacao.MaxHorariosFuncionamento);
    }
}
