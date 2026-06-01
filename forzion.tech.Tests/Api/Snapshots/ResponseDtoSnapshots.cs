using forzion.tech.Application.UseCases.Admin.HealthReport;
using forzion.tech.Application.UseCases.Alunos;
using forzion.tech.Application.UseCases.AssinaturaAlunos;
using forzion.tech.Application.UseCases.Auth.Login;
using forzion.tech.Application.UseCases.Conta.ObterPerfil;
using forzion.tech.Application.UseCases.Pacotes;
using forzion.tech.Application.UseCases.Pagamentos;
using forzion.tech.Application.UseCases.Planos;
using forzion.tech.Application.UseCases.Treinadores;
using forzion.tech.Application.UseCases.Treinadores.VerificarOnboarding;
using forzion.tech.Application.UseCases.Vinculos;
using forzion.tech.Application.UseCases.Vinculos.ObterVinculoAluno;
using forzion.tech.Domain.Enums;
using static VerifyXunit.Verifier;

namespace forzion.tech.Tests.Api.Snapshots;

/// <summary>
/// Snapshots de contrato dos principais response DTOs. Valores deterministicos travam o
/// shape e a serializacao — qualquer alteracao de contrato falha ate re-aprovacao explicita.
/// </summary>
public class ResponseDtoSnapshots
{
    private static readonly Guid Id1 = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Id2 = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Id3 = new("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Id4 = new("44444444-4444-4444-4444-444444444444");
    private static readonly DateTime Criado = new(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Atualizado = new(2026, 1, 2, 9, 30, 0, DateTimeKind.Utc);

    [Fact]
    public Task AlunoResponse() => Verify(new AlunoResponse(
        AlunoId: Id1,
        Nome: "Maria Silva",
        Email: "maria@forzion.tech",
        Telefone: "11999990000",
        Status: AlunoStatus.Ativo,
        ContaId: Id2,
        CreatedAt: Criado,
        UpdatedAt: Atualizado,
        DiasDisponiveis: 4,
        TempoDisponivelMinutos: TempoDisponivel.UmaHora,
        Finalidade: FinalidadeTreino.Hipertrofia,
        FocoTreino: "Membros superiores",
        NivelCondicionamento: NivelCondicionamento.Intermediario,
        LimitacoesFisicas: null,
        Doencas: null,
        ObservacoesAdicionais: null));

    [Fact]
    public Task TreinadorResponse() => Verify(new TreinadorResponse(
        TreinadorId: Id1,
        ContaId: Id2,
        Nome: "Joao Personal",
        Status: TreinadorStatus.Ativo,
        PlanoPlataformaId: Id3,
        CreatedAt: Criado));

    [Fact]
    public Task PacoteResponse() => Verify(new PacoteResponse(
        PacoteId: Id1,
        TreinadorId: Id2,
        Nome: "Plano Mensal",
        Descricao: "Acompanhamento mensal completo",
        Preco: 149.90m,
        IsAtivo: true,
        CreatedAt: Criado,
        UpdatedAt: Atualizado));

    [Fact]
    public Task AssinaturaAlunoResponse() => Verify(new AssinaturaAlunoResponse(
        AssinaturaAlunoId: Id1,
        VinculoId: Id2,
        PacoteId: Id3,
        TreinadorId: Id4,
        AlunoId: Id1,
        Valor: 149.90m,
        Status: AssinaturaAlunoStatus.Ativa,
        DataInicio: Criado,
        DataProximaCobranca: Atualizado,
        DataCancelamento: null,
        CreatedAt: Criado));

    [Fact]
    public Task LoginResponse() => Verify(new LoginResponse(
        Token: "token-jwt-deterministico",
        TipoConta: TipoConta.Aluno,
        ContaId: Id1,
        PerfilId: Id2));

    [Fact]
    public Task PagamentoResponse_Aluno_IncluiClientSecret() => Verify(new PagamentoResponse(
        PagamentoId: Id1,
        AssinaturaAlunoId: Id2,
        Valor: 149.90m,
        Status: PagamentoStatus.Pendente,
        MetodoPagamento: MetodoPagamento.Pix,
        PixQrCode: "00020126pix-qrcode",
        PixQrCodeUrl: "https://forzion.tech/pix/qr",
        PixExpiracao: Atualizado,
        ClientSecret: "pi_secret_deterministico",
        DataPagamento: null,
        CreatedAt: Criado));

    // F33 (Fase 5) — DTOs adicionais que faltavam.

    [Fact]
    public Task PagamentoResponse_Treinador_OmiteClientSecret() => Verify(new PagamentoResponse(
        PagamentoId: Id1,
        AssinaturaAlunoId: Id2,
        Valor: 149.90m,
        Status: PagamentoStatus.Pago,
        MetodoPagamento: MetodoPagamento.Cartao,
        PixQrCode: null,
        PixQrCodeUrl: null,
        PixExpiracao: null,
        ClientSecret: null,
        DataPagamento: Atualizado,
        CreatedAt: Criado));

    [Fact]
    public Task VinculoResponse() => Verify(new VinculoResponse(
        VinculoId: Id1,
        TreinadorId: Id2,
        AlunoId: Id3,
        PacoteId: Id4,
        Status: VinculoStatus.Ativo,
        CreatedAt: Criado));

    [Fact]
    public Task VinculoAlunoItemResponse() => Verify(new VinculoAlunoItemResponse(
        VinculoId: Id1,
        TreinadorId: Id2,
        NomeTreinador: "Coach Silva",
        Status: VinculoStatus.Ativo,
        DataInicio: Criado,
        CreatedAt: Criado));

    [Fact]
    public Task PerfilResponse() => Verify(new PerfilResponse(
        Nome: "Arthur Webster",
        Email: "arthur@forzion.tech",
        TipoConta: "Aluno"));

    [Fact]
    public Task PlanoPlataformaResponse() => Verify(new PlanoPlataformaResponse(
        PlanoId: Id1,
        Nome: "Pro",
        Tier: TierPlano.Pro,
        MaxAlunos: 50,
        Preco: 299.90m,
        IsAtivo: true,
        CreatedAt: Criado,
        UpdatedAt: Atualizado,
        Descricao: "Plano profissional"));

    [Fact]
    public Task OnboardingStatusResponse() => Verify(new OnboardingStatusResponse(
        OnboardingCompleto: true,
        ContaConfigurada: true));

    [Fact]
    public Task HealthSnapshotResponse() => Verify(new HealthSnapshotResponse(
        Id: Id1,
        CapturadoEm: Criado,
        Ambiente: "Homolog",
        StatusGeral: StatusSaude.Ok,
        PayloadJson: "{\"liveness\":\"ok\"}"));

    [Fact]
    public Task HealthReportConfigResponse() => Verify(new HealthReportConfigResponse(
        Id: Id1,
        Ativo: true,
        HoraEnvioUtc: new TimeOnly(8, 30, 0),
        Destinatarios: new[] { "ops@forzion.tech", "dev@forzion.tech" },
        IncluirLiveness: true,
        IncluirKpis: true,
        IncluirEntregabilidade: true,
        IncluirErros: false,
        UltimoEnvioEm: Atualizado));
}
