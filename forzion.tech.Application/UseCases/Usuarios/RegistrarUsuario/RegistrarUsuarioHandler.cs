using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Usuarios.RegistrarUsuario;

public class RegistrarUsuarioHandler(
    IUsuarioRepository usuarioRepository,
    ITenantRepository tenantRepository,
    IPlanoRepository planoRepository,
    IUnitOfWork unitOfWork,
    ILogger<RegistrarUsuarioHandler> logger)
{
    private readonly IUsuarioRepository _usuarioRepository = usuarioRepository;
    private readonly ITenantRepository _tenantRepository = tenantRepository;
    private readonly IPlanoRepository _planoRepository = planoRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<RegistrarUsuarioHandler> _logger = logger;

    public virtual async Task<RegistrarUsuarioResponse> HandleAsync(
        RegistrarUsuarioCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var jaExiste = await _usuarioRepository.ExisteAsync(command.SupabaseId, cancellationToken).ConfigureAwait(false);
        if (jaExiste)
            throw new UsuarioJaRegistradoException();

        var planoFree = await _planoRepository.ObterPlanoFreeAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new PlanoNaoEncontradoException();

        var slug = await GerarSlugUnicoAsync(command.TenantNome, cancellationToken).ConfigureAwait(false);
        var tenant = Tenant.Criar(command.TenantNome, slug, planoFree.Id);
        await _tenantRepository.AdicionarAsync(tenant, cancellationToken).ConfigureAwait(false);

        var email = Email.Criar(command.Email);
        var usuario = Usuario.Criar(command.SupabaseId, command.Nome, email, tenant.Id);
        await _usuarioRepository.AdicionarAsync(usuario, cancellationToken).ConfigureAwait(false);

        await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Usuário {UsuarioId} registrado com tenant {TenantId}.", usuario.Id, tenant.Id);

        return new RegistrarUsuarioResponse(
            usuario.Id,
            usuario.Nome,
            usuario.Email.Value,
            usuario.Role,
            tenant.Id,
            tenant.Nome
        );
    }

    private async Task<Slug> GerarSlugUnicoAsync(string nome, CancellationToken cancellationToken)
    {
        var baseSlug = Slug.FromNome(nome);
        var slug = baseSlug;
        var tentativa = 0;

        while (await _tenantRepository.SlugExisteAsync(slug, cancellationToken).ConfigureAwait(false))
        {
            if (tentativa >= 5)
                throw new DomainException("Não foi possível gerar um slug único para o tenant.");

            slug = Slug.Reconstituir($"{baseSlug.Value}-{Guid.NewGuid().ToString()[..8]}");
            tentativa++;
        }

        return slug;
    }
}
