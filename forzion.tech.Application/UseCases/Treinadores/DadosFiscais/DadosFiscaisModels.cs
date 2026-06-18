using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Treinadores.DadosFiscais;

public record DefinirDadosFiscaisTreinadorCommand(
    Guid TreinadorId,
    TipoDocumentoFiscal TipoDocumento,
    string Documento,
    string RazaoSocial,
    string Logradouro,
    string Numero,
    string Bairro,
    string CodigoMunicipioIbge,
    string Uf,
    string Cep,
    string? Complemento = null,
    string? InscricaoMunicipal = null);

public record EnderecoFiscalResponse(
    string Logradouro,
    string Numero,
    string? Complemento,
    string Bairro,
    string CodigoMunicipioIbge,
    string Uf,
    string Cep);

public record DadosFiscaisResponse(
    TipoDocumentoFiscal TipoDocumento,
    string Documento,
    string RazaoSocial,
    string? InscricaoMunicipal,
    EnderecoFiscalResponse Endereco);
