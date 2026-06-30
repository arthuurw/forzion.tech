using forzion.tech.Domain.Shared;

namespace forzion.tech.Application.Interfaces;

public interface IConsultaCepService
{
    Task<Result<ConsultaCepResultado>> ConsultarAsync(string cep, CancellationToken cancellationToken);
}

public sealed record ConsultaCepResultado(
    string Logradouro,
    string Complemento,
    string Bairro,
    string Localidade,
    string Uf,
    string CodigoMunicipioIbge);
