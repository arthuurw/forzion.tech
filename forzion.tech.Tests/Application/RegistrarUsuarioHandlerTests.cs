using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Usuarios.RegistrarUsuario;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application;

public class RegistrarUsuarioHandlerTests
{
    private readonly Mock<IUsuarioRepository> _usuarioRepo = new();
    private readonly Mock<ITenantRepository> _tenantRepo = new();
    private readonly Mock<IPlanoRepository> _planoRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<RegistrarUsuarioHandler>> _logger = new();
    private readonly RegistrarUsuarioHandler _handler;

    private static readonly Plano PlanoFree =
        Plano.CriarComId(Guid.NewGuid(), "Free", 0, 5, true);

    public RegistrarUsuarioHandlerTests()
    {
        _handler = new RegistrarUsuarioHandler(
            _usuarioRepo.Object,
            _tenantRepo.Object,
            _planoRepo.Object,
            _unitOfWork.Object,
            _logger.Object);
    }

    private RegistrarUsuarioCommand ComandoValido() => new(
        Guid.NewGuid(), "João Silva", "joao@example.com", "Academia Força");

    [Fact]
    public async Task HandleAsync_DadosValidos_RetornaResponse()
    {
        var command = ComandoValido();
        _usuarioRepo.Setup(r => r.ExisteAsync(command.SupabaseId, default)).ReturnsAsync(false);
        _planoRepo.Setup(r => r.ObterPlanoFreeAsync(default)).ReturnsAsync(PlanoFree);
        _tenantRepo.Setup(r => r.SlugExisteAsync(It.IsAny<Slug>(), default)).ReturnsAsync(false);

        var result = await _handler.HandleAsync(command);

        result.Should().NotBeNull();
        result.UsuarioId.Should().Be(command.SupabaseId);
        result.Nome.Should().Be("João Silva");
        result.Email.Should().Be("joao@example.com");
        result.Role.Should().Be(Role.Admin);
        result.TenantNome.Should().Be("Academia Força");
    }

    [Fact]
    public async Task HandleAsync_DadosValidos_PersisteTenantEUsuario()
    {
        var command = ComandoValido();
        _usuarioRepo.Setup(r => r.ExisteAsync(command.SupabaseId, default)).ReturnsAsync(false);
        _planoRepo.Setup(r => r.ObterPlanoFreeAsync(default)).ReturnsAsync(PlanoFree);
        _tenantRepo.Setup(r => r.SlugExisteAsync(It.IsAny<Slug>(), default)).ReturnsAsync(false);

        await _handler.HandleAsync(command);

        _tenantRepo.Verify(r => r.AdicionarAsync(It.IsAny<Tenant>(), default), Times.Once);
        _usuarioRepo.Verify(r => r.AdicionarAsync(It.IsAny<Usuario>(), default), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_UsuarioJaExiste_LancaUsuarioJaRegistradoException()
    {
        var command = ComandoValido();
        _usuarioRepo.Setup(r => r.ExisteAsync(command.SupabaseId, default)).ReturnsAsync(true);

        var act = async () => await _handler.HandleAsync(command);

        await act.Should().ThrowAsync<UsuarioJaRegistradoException>();
        _unitOfWork.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PlanoFreeNaoEncontrado_LancaPlanoNaoEncontradoException()
    {
        var command = ComandoValido();
        _usuarioRepo.Setup(r => r.ExisteAsync(command.SupabaseId, default)).ReturnsAsync(false);
        _planoRepo.Setup(r => r.ObterPlanoFreeAsync(default)).ReturnsAsync((Plano?)null);

        var act = async () => await _handler.HandleAsync(command);

        await act.Should().ThrowAsync<PlanoNaoEncontradoException>();
        _unitOfWork.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SlugJaExiste_GeraSlugComSufixo()
    {
        var command = ComandoValido();
        _usuarioRepo.Setup(r => r.ExisteAsync(command.SupabaseId, default)).ReturnsAsync(false);
        _planoRepo.Setup(r => r.ObterPlanoFreeAsync(default)).ReturnsAsync(PlanoFree);

        var chamadas = 0;
        _tenantRepo.Setup(r => r.SlugExisteAsync(It.IsAny<Slug>(), default))
            .ReturnsAsync(() => chamadas++ < 2);

        Tenant? tenantPersistido = null;
        _tenantRepo.Setup(r => r.AdicionarAsync(It.IsAny<Tenant>(), default))
            .Callback<Tenant, CancellationToken>((t, _) => tenantPersistido = t);

        await _handler.HandleAsync(command);

        tenantPersistido!.Slug.Value.Should().NotBe("academia-forca");
    }

    [Fact]
    public async Task HandleAsync_SlugIndisponivel5Vezes_LancaDomainException()
    {
        var command = ComandoValido();
        _usuarioRepo.Setup(r => r.ExisteAsync(command.SupabaseId, default)).ReturnsAsync(false);
        _planoRepo.Setup(r => r.ObterPlanoFreeAsync(default)).ReturnsAsync(PlanoFree);
        _tenantRepo.Setup(r => r.SlugExisteAsync(It.IsAny<Slug>(), default)).ReturnsAsync(true);

        var act = async () => await _handler.HandleAsync(command);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("Não foi possível gerar um slug único para o tenant.");
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
