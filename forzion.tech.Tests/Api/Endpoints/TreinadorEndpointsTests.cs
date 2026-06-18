using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using FluentAssertions;
using FluentValidation;
using forzion.tech.Application.Auth;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Shared;
using forzion.tech.Application.UseCases.Alunos;
using forzion.tech.Application.UseCases.Alunos.ListarAlunos;
using forzion.tech.Application.UseCases.Admin.GruposMusculares;
using forzion.tech.Application.UseCases.Admin.GruposMusculares.ListarGruposMusculares;
using forzion.tech.Application.UseCases.Alunos.ObterAluno;
using forzion.tech.Application.UseCases.Alunos.ObterProgressaoAluno;
using forzion.tech.Application.UseCases.Exercicios;
using forzion.tech.Application.UseCases.Exercicios.AtualizarExercicio;
using forzion.tech.Application.UseCases.Exercicios.CopiarExercicioGlobal;
using forzion.tech.Application.UseCases.Exercicios.CriarExercicio;
using forzion.tech.Application.UseCases.Exercicios.ExcluirExercicio;
using forzion.tech.Application.UseCases.Exercicios.ListarExercicios;
using forzion.tech.Application.UseCases.Treinos.ListarFichasDoAluno;
using forzion.tech.Application.UseCases.Pacotes;
using forzion.tech.Application.UseCases.Pacotes.AtualizarPacote;
using forzion.tech.Application.UseCases.Pacotes.CriarPacote;
using forzion.tech.Application.UseCases.Pacotes.ExcluirPacote;
using forzion.tech.Application.UseCases.Pacotes.ListarPacotes;
using forzion.tech.Application.UseCases.Pagamentos;
using forzion.tech.Application.UseCases.Pagamentos.GerarCobrancaMensal;
using forzion.tech.Application.UseCases.Treinos.ListarTreinosDoTreinador;
using forzion.tech.Application.UseCases.Treinos.VincularFichaAoAluno;
using forzion.tech.Application.UseCases.Treinadores.CancelarMinhaAssinaturaTreinador;
using forzion.tech.Application.UseCases.Treinadores.AlterarModoPagamento;
using forzion.tech.Application.UseCases.Treinadores.ObterPreviewModoPagamento;
using forzion.tech.Application.UseCases.Treinadores.DadosFiscais;
using forzion.tech.Application.UseCases.Nfse.ListarNotasFiscaisTreinador;
using forzion.tech.Application.UseCases.Nfse.ObterDanfseTreinador;
using forzion.tech.Application.UseCases.Treinadores.IniciarOnboarding;
using forzion.tech.Application.UseCases.Treinadores.VerificarOnboarding;
using forzion.tech.Domain.Shared.Errors;
using forzion.tech.Application.UseCases.Vinculos;
using forzion.tech.Application.UseCases.Vinculos.AprovarVinculo;
using forzion.tech.Application.UseCases.Vinculos.DesvincularAluno;
using forzion.tech.Application.UseCases.Vinculos.ListarVinculos;
using forzion.tech.Application.UseCases.Vinculos.ReativarVinculo;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace forzion.tech.Tests.Api.Endpoints;

public class TreinadorEndpointsTests : IClassFixture<TreinadorEndpointsTests.TreinadorWebFactory>
{
    private readonly TreinadorWebFactory _factory;

    private static readonly Guid TreinadorId = Guid.NewGuid();
    private static readonly Guid ContaId = Guid.NewGuid();
    private static readonly Guid AlunoId = Guid.NewGuid();
    private static readonly Guid VinculoId = Guid.NewGuid();
    private static readonly Guid PacoteId = Guid.NewGuid();

    private static readonly AlunoResponse RespostaAluno = new(
        AlunoId, "João", null, null, AlunoStatus.Ativo, ContaId, DateTime.UtcNow, null);

    private static readonly ListarAlunosResponse RespostaListaAlunos =
        new([RespostaAluno], 1, 1, 20);

    private static readonly VinculoResponse RespostaVinculo = new(
        VinculoId, TreinadorId, AlunoId, PacoteId, VinculoStatus.Ativo, DateTime.UtcNow);

    private static readonly forzion.tech.Application.UseCases.Vinculos.ListarVinculos.VinculoDetalheResponse RespostaVinculoDetalhe = new(
        VinculoId, TreinadorId, AlunoId, PacoteId, VinculoStatus.Ativo, "João", null, DateTime.UtcNow, false);

    private static readonly PacoteResponse RespostaPacote = new(
        PacoteId, TreinadorId, "Pacote Básico", null, 99m, true, DateTime.UtcNow, null);

    private static readonly Guid AssinaturaAlunoId = Guid.NewGuid();
    private static readonly Guid PagamentoId = Guid.NewGuid();

    private static readonly PagamentoResponse RespostaPagamento = new(
        PagamentoId, AssinaturaAlunoId, 99.90m, PagamentoStatus.Pendente, MetodoPagamento.Pix,
        "qrcode", "https://example.com/qr", DateTime.UtcNow.AddHours(1), null, null, DateTime.UtcNow);

    private static readonly ExercicioResponse RespostaExercicio = new(
        Guid.NewGuid(), "Supino", Guid.NewGuid(), "Peito", null, TreinadorId, false, DateTime.UtcNow, null);

    private static readonly GrupoMuscularResponse RespostaGrupoMuscular = new(
        Guid.NewGuid(), "Peitoral", DateTime.UtcNow, null);

    private static readonly ProgressaoAlunoResponse RespostaProgressao = new([]);

    public TreinadorEndpointsTests(TreinadorWebFactory factory)
    {
        _factory = factory;
    }

    private const string StepUpTokenValido = "step-up-ok";

    private HttpClient CriarClienteTreinador()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", "treinador");
        return client;
    }

    private HttpClient CriarClienteTreinadorComStepUp()
    {
        var client = CriarClienteTreinador();
        client.DefaultRequestHeaders.Add("X-Step-Up-Token", StepUpTokenValido);
        return client;
    }

    private HttpClient CriarClienteAdmin()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", "admin");
        return client;
    }

    // --- Auth ---

    [Fact]
    public async Task Get_Alunos_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient().GetAsync("/treinador/alunos");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Alunos_RoleErrada_Retorna403()
    {
        var response = await CriarClienteAdmin().GetAsync("/treinador/alunos");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- GET /treinador/alunos ---

    [Fact]
    public async Task Get_Alunos_Treinador_Retorna200()
    {
        _factory.ListarAlunosHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ListarAlunosQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespostaListaAlunos);

        var response = await CriarClienteTreinador().GetAsync("/treinador/alunos");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- POST /treinador/vinculos/{id}/aprovar ---

    [Fact]
    public async Task Post_AprovarVinculo_Treinador_Retorna200()
    {
        _factory.AprovarVinculoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<AprovarVinculoCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(RespostaVinculo));

        var response = await CriarClienteTreinador().PostAsJsonAsync(
            $"/treinador/vinculos/{VinculoId}/aprovar",
            new { PacoteId = PacoteId, TrarFichas = false });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_AprovarVinculo_NaoEncontrado_Retorna404()
    {
        _factory.AprovarVinculoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<AprovarVinculoCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AlunoNaoEncontradoException());

        var response = await CriarClienteTreinador().PostAsJsonAsync(
            $"/treinador/vinculos/{Guid.NewGuid()}/aprovar",
            new { PacoteId = Guid.NewGuid(), TrarFichas = false });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- POST /treinador/vinculos/{id}/desvincular ---

    [Fact]
    public async Task Post_DesvincularAluno_Treinador_Retorna204()
    {
        _factory.DesvincularAlunoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<DesvincularAlunoCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var response = await CriarClienteTreinador().PostAsJsonAsync(
            $"/treinador/vinculos/{VinculoId}/desvincular",
            new { Observacao = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // --- GET /treinador/vinculos ---

    [Fact]
    public async Task Get_Vinculos_Treinador_Retorna200()
    {
        _factory.ListarVinculosHandlerMock
            .Setup(h => h.HandleAsync(
                It.IsAny<Guid>(), It.IsAny<VinculoStatus?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListarVinculosResponse([RespostaVinculoDetalhe], 1, 1, 20));

        var response = await CriarClienteTreinador().GetAsync("/treinador/vinculos");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- GET /treinador/treinos ---

    [Fact]
    public async Task Get_Treinos_Treinador_Retorna200()
    {
        _factory.ListarTreinosHandlerMock
            .Setup(h => h.HandleAsync(
                It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new forzion.tech.Application.UseCases.Treinos.ListarTreinos.ListarTreinosResponse([], 0, 1, 20));

        var response = await CriarClienteTreinador().GetAsync("/treinador/treinos");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- GET /treinador/exercicios ---

    [Fact]
    public async Task Get_Exercicios_Treinador_Retorna200()
    {
        _factory.ListarExerciciosHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ListarExerciciosQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListarExerciciosResponse([], 0, 1, 20));

        var response = await CriarClienteTreinador().GetAsync("/treinador/exercicios");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- GET /treinador/pacotes ---

    [Fact]
    public async Task Get_Pacotes_Treinador_Retorna200()
    {
        _factory.ListarPacotesHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([RespostaPacote]);

        var response = await CriarClienteTreinador().GetAsync("/treinador/pacotes");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- POST /treinador/pacotes ---

    [Fact]
    public async Task Post_CriarPacote_Treinador_Retorna201()
    {
        _factory.CriarPacoteHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CriarPacoteCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(RespostaPacote));

        var response = await CriarClienteTreinador().PostAsJsonAsync("/treinador/pacotes",
            new { Nome = "Pacote Premium", Preco = 199m });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // --- PATCH /treinador/pacotes/{id} ---

    [Fact]
    public async Task Patch_AtualizarPacote_Treinador_Retorna200()
    {
        _factory.AtualizarPacoteHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<AtualizarPacoteCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(RespostaPacote with { Nome = "Pacote Atualizado" }));

        var response = await CriarClienteTreinador().PatchAsJsonAsync(
            $"/treinador/pacotes/{PacoteId}",
            new { Nome = "Pacote Atualizado", Preco = (decimal?)null, Descricao = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- DELETE /treinador/pacotes/{id} ---

    [Fact]
    public async Task Delete_ExcluirPacote_Treinador_Retorna204()
    {
        _factory.ExcluirPacoteHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ExcluirPacoteCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var response = await CriarClienteTreinador().DeleteAsync($"/treinador/pacotes/{PacoteId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_ExcluirPacote_NaoEncontrado_Retorna404()
    {
        _factory.ExcluirPacoteHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ExcluirPacoteCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PacoteNaoEncontradoException());

        var response = await CriarClienteTreinador().DeleteAsync($"/treinador/pacotes/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- PATCH /treinador/pacotes/{id} error path ---

    [Fact]
    public async Task Patch_AtualizarPacote_NaoEncontrado_Retorna404()
    {
        _factory.AtualizarPacoteHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<AtualizarPacoteCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PacoteNaoEncontradoException());

        var response = await CriarClienteTreinador().PatchAsJsonAsync(
            $"/treinador/pacotes/{Guid.NewGuid()}",
            new { Nome = "X", Preco = (decimal?)null, Descricao = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- POST /treinador/onboarding ---

    [Fact]
    public async Task Post_Onboarding_Treinador_Retorna200()
    {
        _factory.IniciarOnboardingHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<IniciarOnboardingTreinadorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success("http://localhost/stripe-onboarding"));

        var response = await CriarClienteTreinadorComStepUp().PostAsJsonAsync("/treinador/onboarding",
            new { UrlRetorno = "http://localhost/retorno", UrlCancelamento = "http://localhost/cancelar" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_Onboarding_SemStepUp_Retorna403()
    {
        var response = await CriarClienteTreinador().PostAsJsonAsync("/treinador/onboarding",
            new { UrlRetorno = "http://localhost/retorno", UrlCancelamento = "http://localhost/cancelar" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("step_up_requerido");
    }

    [Fact]
    public async Task Post_Onboarding_DomainException_Retorna422()
    {
        _factory.IniciarOnboardingHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<IniciarOnboardingTreinadorCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DomainException("Treinador não encontrado."));

        var response = await CriarClienteTreinadorComStepUp().PostAsJsonAsync("/treinador/onboarding",
            new { UrlRetorno = "http://localhost/retorno", UrlCancelamento = "http://localhost/cancelar" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // --- POST /treinador/modo-pagamento ---

    [Fact]
    public async Task Post_ModoPagamento_Sucesso_Retorna200()
    {
        _factory.AlterarModoPagamentoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<AlterarModoPagamentoTreinadorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new AlterarModoPagamentoResponse(ModoPagamentoAluno.Externo, DateTime.UtcNow)));

        var response = await CriarClienteTreinador().PostAsJsonAsync("/treinador/modo-pagamento", new { Modo = "Externo" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_ModoPagamento_Cooldown_Retorna422()
    {
        _factory.AlterarModoPagamentoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<AlterarModoPagamentoTreinadorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<AlterarModoPagamentoResponse>(
                Error.Business("treinador.cooldown_modo_pagamento", "Aguarde o período de carência.")));

        var response = await CriarClienteTreinador().PostAsJsonAsync("/treinador/modo-pagamento", new { Modo = "Plataforma" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Post_ModoPagamento_RoleErrada_Retorna403()
    {
        var response = await CriarClienteAdmin().PostAsJsonAsync("/treinador/modo-pagamento", new { Modo = "Externo" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AlterarModoPagamento_EnumForaDeRange_Retorna422()
    {
        _factory.AlterarModoPagamentoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<AlterarModoPagamentoTreinadorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<AlterarModoPagamentoResponse>(TreinadorErrors.ModoPagamentoInvalido));

        var corpo = new StringContent("{\"modo\":99}", System.Text.Encoding.UTF8, "application/json");
        var response = await CriarClienteTreinador().PostAsync("/treinador/modo-pagamento", corpo);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.UnprocessableEntity, HttpStatusCode.BadRequest);
    }

    // --- GET /treinador/modo-pagamento/preview ---

    [Fact]
    public async Task Get_PreviewModoPagamento_Treinador_Retorna200()
    {
        _factory.ObterPreviewModoPagamentoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ObterPreviewModoPagamentoTreinadorQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PreviewModoPagamentoResponse(3, 2));

        var response = await CriarClienteTreinador().GetAsync("/treinador/modo-pagamento/preview");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PreviewModoPagamentoResponse>();
        body!.AssinaturasAtivasAlunos.Should().Be(3);
        body.VinculosCobravelSemAssinatura.Should().Be(2);
    }

    [Fact]
    public async Task Get_PreviewModoPagamento_RoleErrada_Retorna403()
    {
        var response = await CriarClienteAdmin().GetAsync("/treinador/modo-pagamento/preview");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- GET /treinador/onboarding/status ---

    [Fact]
    public async Task Get_OnboardingStatus_Treinador_Retorna200()
    {
        _factory.VerificarOnboardingHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<VerificarOnboardingTreinadorQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new OnboardingStatusResponse(true, true, ModoPagamentoAluno.Plataforma, null)));

        var response = await CriarClienteTreinador().GetAsync("/treinador/onboarding/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- POST /treinador/plano/cancelar ---

    [Fact]
    public async Task Post_CancelarPlano_Treinador_Retorna200ComCanceladaEm()
    {
        var canceladaEm = new DateTime(2026, 5, 24, 12, 0, 0, DateTimeKind.Utc);
        _factory.CancelarPlanoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CancelarMinhaAssinaturaTreinadorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new CancelarMinhaAssinaturaTreinadorResponse(canceladaEm)));

        var response = await CriarClienteTreinador().PostAsJsonAsync("/treinador/plano/cancelar", (object?)null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CancelarPlanoResponseDto>();
        body!.CanceladaEm.Should().Be(canceladaEm);
    }

    [Fact]
    public async Task Post_CancelarPlano_ComVinculosAtivos_Retorna409OffboardingNecessario()
    {
        _factory.CancelarPlanoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CancelarMinhaAssinaturaTreinadorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<CancelarMinhaAssinaturaTreinadorResponse>(AssinaturaTreinadorErrors.OffboardingNecessario));

        var response = await CriarClienteTreinador().PostAsJsonAsync("/treinador/plano/cancelar", (object?)null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemWithCode>();
        problem!.Code.Should().Be("assinatura_treinador.offboarding_necessario");
    }

    [Fact]
    public async Task Post_CancelarPlano_SemAssinatura_Retorna404()
    {
        _factory.CancelarPlanoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CancelarMinhaAssinaturaTreinadorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<CancelarMinhaAssinaturaTreinadorResponse>(
                new forzion.tech.Domain.Shared.Error("assinatura_nao_encontrada", "Nenhuma assinatura ativa.", forzion.tech.Domain.Shared.ErrorType.NotFound)));

        var response = await CriarClienteTreinador().PostAsJsonAsync("/treinador/plano/cancelar", (object?)null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed record CancelarPlanoResponseDto(DateTime CanceladaEm);
    private sealed record ProblemWithCode(string? Code);

    // --- POST /treinador/pagamentos/cobrar/{id} ---

    [Fact]
    public async Task Post_Cobrar_Treinador_Retorna200()
    {
        _factory.GerarCobrancaHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<GerarCobrancaMensalCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(RespostaPagamento));

        var response = await CriarClienteTreinador()
            .PostAsJsonAsync($"/treinador/pagamentos/cobrar/{AssinaturaAlunoId}?metodo=Pix", (object?)null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_Cobrar_AssinaturaAlunoCancelada_Retorna422()
    {
        _factory.GerarCobrancaHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<GerarCobrancaMensalCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<PagamentoResponse>(Error.Business("assinatura_aluno.cancelada", "AssinaturaAluno cancelada não pode ser cobrada.")));

        var response = await CriarClienteTreinador()
            .PostAsJsonAsync($"/treinador/pagamentos/cobrar/{AssinaturaAlunoId}?metodo=Pix", (object?)null);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // --- POST /treinador/alunos/{id}/reativar ---

    [Fact]
    public async Task Post_ReativarVinculo_Treinador_Retorna200()
    {
        _factory.ReativarVinculoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ReativarVinculoCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(RespostaVinculo));

        var response = await CriarClienteTreinador().PostAsJsonAsync(
            $"/treinador/alunos/{AlunoId}/reativar",
            new { PacoteId = PacoteId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- POST /treinador/alunos/{id}/fichas/{treinoId} ---

    [Fact]
    public async Task Post_VincularFicha_Treinador_Retorna204()
    {
        _factory.VincularFichaHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<VincularFichaAoAlunoCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var treinoId = Guid.NewGuid();
        var response = await CriarClienteTreinador()
            .PostAsJsonAsync($"/treinador/alunos/{AlunoId}/fichas/{treinoId}", (object?)null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // --- GET /treinador/alunos/{id} ---

    [Fact]
    public async Task Get_AlunoDetalhe_Treinador_Retorna200()
    {
        _factory.ObterAlunoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ObterAlunoQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespostaAluno);

        var response = await CriarClienteTreinador().GetAsync($"/treinador/alunos/{AlunoId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_AlunoDetalhe_NaoEncontrado_Retorna404()
    {
        _factory.ObterAlunoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ObterAlunoQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AlunoNaoEncontradoException());

        var response = await CriarClienteTreinador().GetAsync($"/treinador/alunos/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- GET /treinador/alunos/{id}/fichas ---

    [Fact]
    public async Task Get_FichasDoAluno_Treinador_Retorna200()
    {
        _factory.ListarFichasDoAlunoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TreinoAlunoResponse>());

        var response = await CriarClienteTreinador().GetAsync($"/treinador/alunos/{AlunoId}/fichas");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- GET /treinador/alunos/{id}/progressao ---

    [Fact]
    public async Task Get_ProgressaoAluno_Treinador_Retorna200()
    {
        _factory.ObterProgressaoAlunoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ObterProgressaoAlunoQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespostaProgressao);

        var response = await CriarClienteTreinador().GetAsync($"/treinador/alunos/{AlunoId}/progressao");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_ProgressaoAluno_DataInvalida_Retorna400()
    {
        var ontem = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
        var hoje = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var response = await CriarClienteTreinador()
            .GetAsync($"/treinador/alunos/{AlunoId}/progressao?de={hoje}&ate={ontem}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    // --- GET /treinador/grupos-musculares ---

    [Fact]
    public async Task Get_GruposMusculares_Treinador_Retorna200()
    {
        _factory.ListarGruposMuscularesHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { RespostaGrupoMuscular });

        var response = await CriarClienteTreinador().GetAsync("/treinador/grupos-musculares");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- POST /treinador/exercicios ---

    [Fact]
    public async Task Post_CriarExercicio_Treinador_Retorna201()
    {
        _factory.CriarExercicioHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CriarExercicioCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(RespostaExercicio));

        var response = await CriarClienteTreinador().PostAsJsonAsync("/treinador/exercicios",
            new { nome = "Supino", grupoMuscularId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Post_CriarExercicio_NomeDuplicado_Retorna422()
    {
        _factory.CriarExercicioHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CriarExercicioCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<ExercicioResponse>(Error.Business("exercicio.nome_duplicado", "Já existe um exercício com este nome nesta biblioteca.")));

        var response = await CriarClienteTreinador().PostAsJsonAsync("/treinador/exercicios",
            new { nome = "Supino", grupoMuscularId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // --- POST /treinador/exercicios/{id}/copiar ---

    [Fact]
    public async Task Post_CopiarExercicioGlobal_Retorna201()
    {
        _factory.CopiarExercicioHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CopiarExercicioGlobalCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(RespostaExercicio));

        var response = await CriarClienteTreinador()
            .PostAsJsonAsync($"/treinador/exercicios/{Guid.NewGuid()}/copiar", (object?)null);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Post_CopiarExercicioGlobal_NaoEncontrado_Retorna404()
    {
        _factory.CopiarExercicioHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CopiarExercicioGlobalCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ExercicioNaoEncontradoException());

        var response = await CriarClienteTreinador()
            .PostAsJsonAsync($"/treinador/exercicios/{Guid.NewGuid()}/copiar", (object?)null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- PATCH /treinador/exercicios/{id} ---

    [Fact]
    public async Task Patch_AtualizarExercicio_Treinador_Retorna200()
    {
        _factory.AtualizarExercicioHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<AtualizarExercicioCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(RespostaExercicio));

        var response = await CriarClienteTreinador()
            .PatchAsJsonAsync($"/treinador/exercicios/{Guid.NewGuid()}", new { nome = "Novo Nome" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Patch_AtualizarExercicio_NomeDuplicado_Retorna422()
    {
        _factory.AtualizarExercicioHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<AtualizarExercicioCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<ExercicioResponse>(Error.Business("exercicio.nome_duplicado", "Já existe um exercício com este nome nesta biblioteca.")));

        var response = await CriarClienteTreinador()
            .PatchAsJsonAsync($"/treinador/exercicios/{Guid.NewGuid()}", new { nome = "Supino" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // --- DELETE /treinador/exercicios/{id} ---

    [Fact]
    public async Task Delete_ExcluirExercicio_Treinador_Retorna204()
    {
        _factory.ExcluirExercicioHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ExcluirExercicioCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var response = await CriarClienteTreinador()
            .DeleteAsync($"/treinador/exercicios/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_ExcluirExercicio_EmUso_Retorna422()
    {
        _factory.ExcluirExercicioHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ExcluirExercicioCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.Business("exercicio.em_uso", "Este exercício está em uso em fichas de treino e não pode ser excluído.")));

        var response = await CriarClienteTreinador()
            .DeleteAsync($"/treinador/exercicios/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // --- POST /treinador/vinculos/{id}/desvincular error path ---

    [Fact]
    public async Task Post_DesvincularAluno_NaoEncontrado_Retorna404()
    {
        _factory.DesvincularAlunoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<DesvincularAlunoCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new VinculoNaoEncontradoException());

        var response = await CriarClienteTreinador().PostAsJsonAsync(
            $"/treinador/vinculos/{Guid.NewGuid()}/desvincular",
            new { Observacao = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- POST /treinador/alunos/{id}/reativar error paths ---

    [Fact]
    public async Task Post_ReativarVinculo_NaoEncontrado_Retorna404()
    {
        _factory.ReativarVinculoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ReativarVinculoCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AlunoNaoEncontradoException());

        var response = await CriarClienteTreinador().PostAsJsonAsync(
            $"/treinador/alunos/{Guid.NewGuid()}/reativar",
            new { PacoteId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Post_ReativarVinculo_LimiteAtingido_Retorna422()
    {
        _factory.ReativarVinculoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ReativarVinculoCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new LimiteAlunosAtingidoException());

        var response = await CriarClienteTreinador().PostAsJsonAsync(
            $"/treinador/alunos/{Guid.NewGuid()}/reativar",
            new { PacoteId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // --- POST /treinador/alunos/{id}/fichas/{id} error path ---

    [Fact]
    public async Task Post_VincularFicha_Falha_Retorna422()
    {
        _factory.VincularFichaHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<VincularFichaAoAlunoCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.Business("vinculo.aluno_nao_vinculado", "Aluno não está vinculado ao treinador.")));

        var treinoId = Guid.NewGuid();
        var response = await CriarClienteTreinador()
            .PostAsJsonAsync($"/treinador/alunos/{AlunoId}/fichas/{treinoId}", (object?)null);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // --- POST /treinador/onboarding extra paths ---

    [Fact]
    public async Task Post_Onboarding_UrlForaDominio_Retorna400()
    {
        var response = await CriarClienteTreinadorComStepUp().PostAsJsonAsync("/treinador/onboarding",
            new { UrlRetorno = "http://malicious.com/retorno", UrlCancelamento = "http://localhost/cancelar" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Post_Onboarding_UrlBaseNaoConfigurada_Retorna500()
    {
        var client = _factory.WithWebHostBuilder(b => b.UseSetting("Stripe:UrlBase", "")).CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "treinador");
        client.DefaultRequestHeaders.Add("X-Step-Up-Token", StepUpTokenValido);

        var response = await client.PostAsJsonAsync("/treinador/onboarding",
            new { UrlRetorno = "http://localhost/retorno", UrlCancelamento = "http://localhost/cancelar" });

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Post_Onboarding_Falha_Retorna422()
    {
        _factory.IniciarOnboardingHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<IniciarOnboardingTreinadorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<string>(Error.Business("treinador.nao_encontrado", "Treinador não encontrado.")));

        var response = await CriarClienteTreinadorComStepUp().PostAsJsonAsync("/treinador/onboarding",
            new { UrlRetorno = "http://localhost/retorno", UrlCancelamento = "http://localhost/cancelar" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // --- DELETE /treinador/pacotes/{id} failure path ---

    [Fact]
    public async Task Delete_ExcluirPacote_EmUso_Retorna422()
    {
        _factory.ExcluirPacoteHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ExcluirPacoteCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.Business("pacote.em_uso", "Pacote em uso por assinaturas ativas.")));

        var response = await CriarClienteTreinador().DeleteAsync($"/treinador/pacotes/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    private static object DadosFiscaisBody() => new
    {
        TipoDocumento = "Cpf",
        Documento = "39053344705",
        RazaoSocial = "João Treinador",
        Logradouro = "Rua A",
        Numero = "100",
        Bairro = "Centro",
        CodigoMunicipioIbge = "3550308",
        Uf = "SP",
        Cep = "01001000",
    };

    private static DadosFiscaisResponse RespostaDadosFiscais => new(
        TipoDocumentoFiscal.Cpf, "39053344705", "João Treinador", null,
        new EnderecoFiscalResponse("Rua A", "100", null, "Centro", "3550308", "SP", "01001000"));

    [Fact]
    public async Task Put_DadosFiscais_Treinador_Retorna200()
    {
        _factory.DefinirDadosFiscaisHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<DefinirDadosFiscaisTreinadorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(RespostaDadosFiscais));

        var response = await CriarClienteTreinador().PutAsJsonAsync("/treinador/dados-fiscais", DadosFiscaisBody());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Put_DadosFiscais_DocumentoInvalido_Retorna400()
    {
        _factory.DefinirDadosFiscaisHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<DefinirDadosFiscaisTreinadorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<DadosFiscaisResponse>(DadosFiscaisErrors.DocumentoInvalido));

        var response = await CriarClienteTreinador().PutAsJsonAsync("/treinador/dados-fiscais", DadosFiscaisBody());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_DadosFiscais_RoleErrada_Retorna403()
    {
        var response = await CriarClienteAdmin().PutAsJsonAsync("/treinador/dados-fiscais", DadosFiscaisBody());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_DadosFiscais_Treinador_Retorna200()
    {
        _factory.ObterDadosFiscaisHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<DadosFiscaisResponse?>(RespostaDadosFiscais));

        var response = await CriarClienteTreinador().GetAsync("/treinador/dados-fiscais");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_NotasFiscais_Treinador_Retorna200()
    {
        _factory.ListarNotasFiscaisHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListarNotasFiscaisResponse([], null));

        var response = await CriarClienteTreinador().GetAsync("/treinador/notas-fiscais");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_Danfse_Treinador_Retorna200()
    {
        _factory.ObterDanfseHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success("https://nfse.example/danfse/1.pdf"));

        var response = await CriarClienteTreinador().GetAsync($"/treinador/notas-fiscais/{Guid.NewGuid()}/danfse");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_Danfse_NaoDono_Retorna404()
    {
        _factory.ObterDanfseHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<string>(NotaFiscalErrors.NaoEncontrada));

        var response = await CriarClienteTreinador().GetAsync($"/treinador/notas-fiscais/{Guid.NewGuid()}/danfse");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- WebApplicationFactory ---

    public class TreinadorWebFactory : WebApplicationFactory<Program>
    {
        public Mock<ListarAlunosHandler> ListarAlunosHandlerMock { get; } = new(
            Mock.Of<IAlunoRepository>(),
            Mock.Of<IUserContext>(),
            Mock.Of<ILogger<ListarAlunosHandler>>());

        public Mock<AprovarVinculoHandler> AprovarVinculoHandlerMock { get; } = new(
            Mock.Of<IVinculoTreinadorAlunoRepository>(),
            Mock.Of<ITreinoAlunoRepository>(),
            Mock.Of<ITreinoRepository>(),
            Mock.Of<IAlunoRepository>(),
            Mock.Of<ITreinadorRepository>(),
            Mock.Of<IContaRecebimentoRepository>(),
            Mock.Of<IPacoteRepository>(),
            Mock.Of<ILimiteTreinadorService>(),
            Mock.Of<ILogAprovacaoRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IDbContextTransactionProvider>(),
            TimeProvider.System,
            Mock.Of<ILogger<AprovarVinculoHandler>>());

        public Mock<DesvincularAlunoHandler> DesvincularAlunoHandlerMock { get; } = new(
            Mock.Of<IVinculoTreinadorAlunoRepository>(),
            Mock.Of<ITreinoAlunoRepository>(),
            Mock.Of<IAssinaturaAlunoRepository>(),
            Mock.Of<ILogAprovacaoRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IUserContext>(), TimeProvider.System,
            Mock.Of<ILogger<DesvincularAlunoHandler>>());

        public Mock<ListarVinculosHandler> ListarVinculosHandlerMock { get; } = new(
            Mock.Of<IVinculoTreinadorAlunoRepository>());

        public Mock<ListarTreinosDoTreinadorHandler> ListarTreinosHandlerMock { get; } = new(
            Mock.Of<ITreinoRepository>(),
            Mock.Of<IExercicioRepository>());

        public Mock<ListarExerciciosHandler> ListarExerciciosHandlerMock { get; } = new(
            Mock.Of<IExercicioRepository>(),
            Mock.Of<IGrupoMuscularRepository>(),
            Mock.Of<ILogger<ListarExerciciosHandler>>());

        public Mock<ListarPacotesHandler> ListarPacotesHandlerMock { get; } = new(
            Mock.Of<IPacoteRepository>());

        public Mock<CriarPacoteHandler> CriarPacoteHandlerMock { get; } = new(
            Mock.Of<IPacoteRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IValidator<CriarPacoteCommand>>(), TimeProvider.System,
            Mock.Of<ILogger<CriarPacoteHandler>>());

        public Mock<AtualizarPacoteHandler> AtualizarPacoteHandlerMock { get; } = new(
            Mock.Of<IPacoteRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IValidator<AtualizarPacoteCommand>>(), TimeProvider.System);

        public Mock<ExcluirPacoteHandler> ExcluirPacoteHandlerMock { get; } = new(
            Mock.Of<IPacoteRepository>(),
            Mock.Of<IUnitOfWork>());

        public Mock<IniciarOnboardingTreinadorHandler> IniciarOnboardingHandlerMock { get; } = new(
            Mock.Of<ITreinadorRepository>(),
            Mock.Of<IContaRecebimentoRepository>(),
            Mock.Of<IContaRepository>(),
            Mock.Of<IStripeService>(),
            Mock.Of<IUnitOfWork>(), TimeProvider.System,
            Mock.Of<ILogger<IniciarOnboardingTreinadorHandler>>());

        public Mock<AlterarModoPagamentoTreinadorHandler> AlterarModoPagamentoHandlerMock { get; } = new(
            Mock.Of<ITreinadorRepository>(),
            Mock.Of<IContaRecebimentoRepository>(),
            Mock.Of<IAssinaturaAlunoRepository>(),
            Mock.Of<IPagamentoRepository>(),
            Mock.Of<IVinculoTreinadorAlunoRepository>(),
            new forzion.tech.Application.Services.CriarAssinaturaAlunoService(
                Mock.Of<IPacoteRepository>(), Mock.Of<IAssinaturaAlunoRepository>(),
                Mock.Of<ILogger<forzion.tech.Application.Services.CriarAssinaturaAlunoService>>()),
            Mock.Of<IStripeService>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IDbContextTransactionProvider>(),
            Mock.Of<IValidator<AlterarModoPagamentoTreinadorCommand>>(),
            TimeProvider.System,
            Mock.Of<ILogger<AlterarModoPagamentoTreinadorHandler>>());

        public Mock<ObterPreviewModoPagamentoTreinadorHandler> ObterPreviewModoPagamentoHandlerMock { get; } = new(
            Mock.Of<IAssinaturaAlunoRepository>(),
            Mock.Of<IVinculoTreinadorAlunoRepository>());

        public Mock<CancelarMinhaAssinaturaTreinadorHandler> CancelarPlanoHandlerMock { get; } = new(
            Mock.Of<IAssinaturaTreinadorRepository>(),
            Mock.Of<IVinculoTreinadorAlunoRepository>(),
            Mock.Of<IPagamentoTreinadorRepository>(),
            new forzion.tech.Application.Services.ReembolsoArrependimentoService(
                Mock.Of<IStripeService>(),
                Mock.Of<ILogger<forzion.tech.Application.Services.ReembolsoArrependimentoService>>()),
            Mock.Of<IUnitOfWork>(), TimeProvider.System,
            Mock.Of<ILogger<CancelarMinhaAssinaturaTreinadorHandler>>());

        public Mock<VerificarOnboardingTreinadorHandler> VerificarOnboardingHandlerMock { get; } = new(
            Mock.Of<ITreinadorRepository>(),
            Mock.Of<IContaRecebimentoRepository>(),
            Mock.Of<IStripeService>(),
            Mock.Of<IUnitOfWork>(), TimeProvider.System,
            Mock.Of<ILogger<VerificarOnboardingTreinadorHandler>>());

        public Mock<GerarCobrancaMensalHandler> GerarCobrancaHandlerMock { get; } = new(
            Mock.Of<IAssinaturaAlunoRepository>(),
            Mock.Of<IPagamentoRepository>(),
            Mock.Of<IContaRecebimentoRepository>(),
            Mock.Of<IStripeService>(),
            new forzion.tech.Application.Services.CriarPagamentoComIntentService(
                Mock.Of<IUnitOfWork>(), Mock.Of<IDbContextTransactionProvider>(),
                TimeProvider.System,
                Mock.Of<ILogger<forzion.tech.Application.Services.CriarPagamentoComIntentService>>()),
            Microsoft.Extensions.Options.Options.Create(new PaymentSettings()), TimeProvider.System,
            Mock.Of<ILogger<GerarCobrancaMensalHandler>>());

        public Mock<ReativarVinculoHandler> ReativarVinculoHandlerMock { get; } = new(
            Mock.Of<IVinculoTreinadorAlunoRepository>(),
            Mock.Of<IAlunoRepository>(),
            Mock.Of<IPacoteRepository>(),
            Mock.Of<ILimiteTreinadorService>(),
            Mock.Of<ILogAprovacaoRepository>(),
            Mock.Of<IUnitOfWork>(), TimeProvider.System,
            Mock.Of<ILogger<ReativarVinculoHandler>>());

        public Mock<VincularFichaAoAlunoHandler> VincularFichaHandlerMock { get; } = new(
            Mock.Of<ITreinoRepository>(),
            Mock.Of<ITreinoAlunoRepository>(),
            Mock.Of<IVinculoTreinadorAlunoRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IUserContext>(), TimeProvider.System,
            Mock.Of<ILogger<VincularFichaAoAlunoHandler>>());

        public Mock<ObterAlunoHandler> ObterAlunoHandlerMock { get; } = new(
            Mock.Of<IAlunoRepository>(),
            Mock.Of<IVinculoTreinadorAlunoRepository>(),
            Mock.Of<IUserContext>(),
            Mock.Of<ILogger<ObterAlunoHandler>>());

        public Mock<ListarFichasDoAlunoHandler> ListarFichasDoAlunoHandlerMock { get; } = new(
            Mock.Of<ITreinoAlunoRepository>());

        public Mock<ObterProgressaoAlunoHandler> ObterProgressaoAlunoHandlerMock { get; } = new(
            Mock.Of<IExecucaoTreinoRepository>(),
            Mock.Of<IVinculoTreinadorAlunoRepository>(),
            Mock.Of<IUserContext>());

        public Mock<ListarGruposMuscularesHandler> ListarGruposMuscularesHandlerMock { get; } = new(
            Mock.Of<IGrupoMuscularRepository>());

        public Mock<CopiarExercicioGlobalHandler> CopiarExercicioHandlerMock { get; } = new(
            Mock.Of<IExercicioRepository>(),
            Mock.Of<IGrupoMuscularRepository>(),
            Mock.Of<IUnitOfWork>(), TimeProvider.System,
            Mock.Of<ILogger<CopiarExercicioGlobalHandler>>());

        public Mock<CriarExercicioHandler> CriarExercicioHandlerMock { get; } = new(
            Mock.Of<IExercicioRepository>(),
            Mock.Of<IGrupoMuscularRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IValidator<CriarExercicioCommand>>(), TimeProvider.System,
            Mock.Of<ILogger<CriarExercicioHandler>>());

        public Mock<AtualizarExercicioHandler> AtualizarExercicioHandlerMock { get; } = new(
            Mock.Of<IExercicioRepository>(),
            Mock.Of<IGrupoMuscularRepository>(),
            Mock.Of<IUnitOfWork>(), TimeProvider.System);

        public Mock<ExcluirExercicioHandler> ExcluirExercicioHandlerMock { get; } = new(
            Mock.Of<IExercicioRepository>(),
            Mock.Of<IUnitOfWork>());

        public Mock<DefinirDadosFiscaisTreinadorHandler> DefinirDadosFiscaisHandlerMock { get; } = new(
            Mock.Of<ITreinadorRepository>(),
            Mock.Of<IUnitOfWork>(), TimeProvider.System);

        public Mock<ObterDadosFiscaisTreinadorHandler> ObterDadosFiscaisHandlerMock { get; } = new(
            Mock.Of<ITreinadorRepository>());

        public Mock<ListarNotasFiscaisTreinadorHandler> ListarNotasFiscaisHandlerMock { get; } = new(
            Mock.Of<INotaFiscalRepository>());

        public Mock<ObterDanfseTreinadorHandler> ObterDanfseHandlerMock { get; } = new(
            Mock.Of<INotaFiscalRepository>());

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("AllowedHosts", "*");
            builder.UseSetting("Auth:JwtSecret", "test-only-secret-at-least-32-chars!!");
            builder.UseSetting("Stripe:UrlBase", "http://localhost");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ListarAlunosHandler>();
                services.RemoveAll<AprovarVinculoHandler>();
                services.RemoveAll<DesvincularAlunoHandler>();
                services.RemoveAll<ListarVinculosHandler>();
                services.RemoveAll<ListarTreinosDoTreinadorHandler>();
                services.RemoveAll<ListarExerciciosHandler>();
                services.RemoveAll<ListarPacotesHandler>();
                services.RemoveAll<CriarPacoteHandler>();
                services.RemoveAll<AtualizarPacoteHandler>();
                services.RemoveAll<ExcluirPacoteHandler>();
                services.RemoveAll<IniciarOnboardingTreinadorHandler>();
                services.RemoveAll<AlterarModoPagamentoTreinadorHandler>();
                services.RemoveAll<ObterPreviewModoPagamentoTreinadorHandler>();
                services.RemoveAll<CancelarMinhaAssinaturaTreinadorHandler>();
                services.RemoveAll<VerificarOnboardingTreinadorHandler>();
                services.RemoveAll<GerarCobrancaMensalHandler>();
                services.RemoveAll<ReativarVinculoHandler>();
                services.RemoveAll<VincularFichaAoAlunoHandler>();
                services.RemoveAll<ObterAlunoHandler>();
                services.RemoveAll<ListarFichasDoAlunoHandler>();
                services.RemoveAll<ObterProgressaoAlunoHandler>();
                services.RemoveAll<ListarGruposMuscularesHandler>();
                services.RemoveAll<CopiarExercicioGlobalHandler>();
                services.RemoveAll<CriarExercicioHandler>();
                services.RemoveAll<AtualizarExercicioHandler>();
                services.RemoveAll<ExcluirExercicioHandler>();
                services.RemoveAll<DefinirDadosFiscaisTreinadorHandler>();
                services.RemoveAll<ObterDadosFiscaisTreinadorHandler>();
                services.RemoveAll<ListarNotasFiscaisTreinadorHandler>();
                services.RemoveAll<ObterDanfseTreinadorHandler>();
                services.RemoveAll<IUserContext>();
                services.RemoveAll<IJwtService>();
                services.RemoveAll<ITokenRevogadoRepository>();

                services.AddScoped(_ => ListarAlunosHandlerMock.Object);
                services.AddScoped(_ => AprovarVinculoHandlerMock.Object);
                services.AddScoped(_ => DesvincularAlunoHandlerMock.Object);
                services.AddScoped(_ => ListarVinculosHandlerMock.Object);
                services.AddScoped(_ => ListarTreinosHandlerMock.Object);
                services.AddScoped(_ => ListarExerciciosHandlerMock.Object);
                services.AddScoped(_ => ListarPacotesHandlerMock.Object);
                services.AddScoped(_ => CriarPacoteHandlerMock.Object);
                services.AddScoped(_ => AtualizarPacoteHandlerMock.Object);
                services.AddScoped(_ => ExcluirPacoteHandlerMock.Object);
                services.AddScoped(_ => IniciarOnboardingHandlerMock.Object);
                services.AddScoped(_ => AlterarModoPagamentoHandlerMock.Object);
                services.AddScoped(_ => ObterPreviewModoPagamentoHandlerMock.Object);
                services.AddScoped(_ => CancelarPlanoHandlerMock.Object);
                services.AddScoped(_ => VerificarOnboardingHandlerMock.Object);
                services.AddScoped(_ => GerarCobrancaHandlerMock.Object);
                services.AddScoped(_ => ReativarVinculoHandlerMock.Object);
                services.AddScoped(_ => VincularFichaHandlerMock.Object);
                services.AddScoped(_ => ObterAlunoHandlerMock.Object);
                services.AddScoped(_ => ListarFichasDoAlunoHandlerMock.Object);
                services.AddScoped(_ => ObterProgressaoAlunoHandlerMock.Object);
                services.AddScoped(_ => ListarGruposMuscularesHandlerMock.Object);
                services.AddScoped(_ => CopiarExercicioHandlerMock.Object);
                services.AddScoped(_ => CriarExercicioHandlerMock.Object);
                services.AddScoped(_ => AtualizarExercicioHandlerMock.Object);
                services.AddScoped(_ => ExcluirExercicioHandlerMock.Object);
                services.AddScoped(_ => DefinirDadosFiscaisHandlerMock.Object);
                services.AddScoped(_ => ObterDadosFiscaisHandlerMock.Object);
                services.AddScoped(_ => ListarNotasFiscaisHandlerMock.Object);
                services.AddScoped(_ => ObterDanfseHandlerMock.Object);

                var userContextMock = new Mock<IUserContext>();
                userContextMock.Setup(u => u.ContaId).Returns(ContaId);
                userContextMock.Setup(u => u.PerfilId).Returns(TreinadorId);
                userContextMock.Setup(u => u.TipoConta).Returns(TipoConta.Treinador);
                services.AddScoped(_ => userContextMock.Object);

                var jwtMock = new Mock<IJwtService>();
                jwtMock.Setup(j => j.ValidarTokenEscopo("step-up-ok", MfaScopes.StepUp))
                    .Returns(new EscopoValidado(ContaId, Guid.NewGuid()));
                services.AddScoped(_ => jwtMock.Object);

                var tokenRevogadoMock = new Mock<ITokenRevogadoRepository>();
                tokenRevogadoMock.Setup(r => r.EstaRevogadoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(false);
                services.AddScoped(_ => tokenRevogadoMock.Object);

                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TreinadorTestAuthHandler>("Test", _ => { });
            });
        }
    }

    public class TreinadorTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TreinadorTestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var header = Request.Headers.Authorization.FirstOrDefault();
            if (string.IsNullOrEmpty(header))
                return Task.FromResult(AuthenticateResult.Fail("Sem token"));

            var param = header.Replace("Test ", "");

            string tipoConta;
            string userId;

            if (param == "treinador")
            {
                tipoConta = "Treinador";
                userId = TreinadorId.ToString();
            }
            else if (param == "admin")
            {
                tipoConta = "SystemAdmin";
                userId = Guid.NewGuid().ToString();
            }
            else
            {
                return Task.FromResult(AuthenticateResult.Fail("Token inválido"));
            }

            var claims = new[]
            {
                new Claim("sub", userId),
                new Claim("tipo_conta", tipoConta),
                new Claim("perfil_id", TreinadorId.ToString())
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "Test");

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
