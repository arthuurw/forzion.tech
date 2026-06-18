using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinadores.ObterPreviewModoPagamento;
using forzion.tech.Domain.Entities;
using forzion.tech.Tests.Builders;
using Moq;

namespace forzion.tech.Tests.Application.Treinadores;

public class ObterPreviewModoPagamentoTreinadorHandlerTests
{
    private readonly Mock<IAssinaturaAlunoRepository> _assinaturaRepo = new();
    private readonly Mock<IVinculoTreinadorAlunoRepository> _vinculoRepo = new();
    private readonly Guid _treinadorId = TestData.NextGuid();

    private ObterPreviewModoPagamentoTreinadorHandler CriarHandler() =>
        new(_assinaturaRepo.Object, _vinculoRepo.Object);

    private void SetAssinaturas(params AssinaturaAluno[] assinaturas) =>
        _assinaturaRepo.Setup(r => r.ListarNaoCanceladasPorTreinadorAsync(_treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinaturas);

    private void SetVinculos(params VinculoTreinadorAluno[] vinculos) =>
        _vinculoRepo.Setup(r => r.ListarAtivosPorTreinadorAsync(_treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vinculos);

    [Fact]
    public async Task AssinaturasAtivas_ContaAsNaoCanceladas()
    {
        SetAssinaturas(
            new AssinaturaAlunoBuilder().ComTreinadorId(_treinadorId).Build(),
            new AssinaturaAlunoBuilder().ComTreinadorId(_treinadorId).Build());
        SetVinculos();

        var result = await CriarHandler().HandleAsync(new ObterPreviewModoPagamentoTreinadorQuery(_treinadorId));

        result.Value.AssinaturasAtivasAlunos.Should().Be(2);
    }

    [Fact]
    public async Task VinculosCobravel_ContaApenasComPacoteESemAssinatura()
    {
        var comPacote = new VinculoTreinadorAlunoBuilder().ComTreinadorId(_treinadorId).ComPacoteId(TestData.NextGuid()).Build();
        var coberto = new VinculoTreinadorAlunoBuilder().ComTreinadorId(_treinadorId).ComPacoteId(TestData.NextGuid()).Build();
        var semPacote = new VinculoTreinadorAlunoBuilder().ComTreinadorId(_treinadorId).ComPacoteId(null).Build();

        SetAssinaturas(new AssinaturaAlunoBuilder().ComTreinadorId(_treinadorId).ComVinculoId(coberto.Id).Build());
        SetVinculos(comPacote, coberto, semPacote);

        var result = await CriarHandler().HandleAsync(new ObterPreviewModoPagamentoTreinadorQuery(_treinadorId));

        result.Value.VinculosCobravelSemAssinatura.Should().Be(1);
        result.Value.AssinaturasAtivasAlunos.Should().Be(1);
    }

    [Fact]
    public async Task SemVinculosNemAssinaturas_RetornaZeros()
    {
        SetAssinaturas();
        SetVinculos();

        var result = await CriarHandler().HandleAsync(new ObterPreviewModoPagamentoTreinadorQuery(_treinadorId));

        result.Value.Should().Be(new PreviewModoPagamentoResponse(0, 0));
    }
}
