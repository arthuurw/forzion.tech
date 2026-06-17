using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Application.Interfaces;

public interface IEmissorNfseService
{
    Task<NfseResultado> EmitirAsync(DpsInput input, CancellationToken cancellationToken = default);
    Task<NfseStatus> ConsultarAsync(string chaveAcesso, CancellationToken cancellationToken = default);
    Task<NfseResultado> CancelarAsync(string chaveAcesso, string motivo, CancellationToken cancellationToken = default);
}

public record DpsPrestador(
    string Cnpj,
    string? InscricaoMunicipal,
    string CodigoMunicipioIbge,
    string RegimeTributario);

// NumeroDpsEstavel: série+número derivado do pagamento/competência (idempotência) — reemissão
// gera o MESMO DPS, gov dedup. Tomador = VO de domínio (sem remodelar no contrato).
public record DpsInput(
    DpsPrestador Prestador,
    DadosFiscais Tomador,
    string CodigoServico,
    decimal Aliquota,
    decimal Valor,
    DateOnly Competencia,
    string NumeroDpsEstavel);

// Sucesso=false ⇒ rejeição do gov (CodigoErro/MotivoErro preenchidos), distinta de exceção
// (timeout/5xx) que propaga p/ retry do outbox.
public record NfseResultado(
    bool Sucesso,
    string? ChaveAcesso,
    string? NumeroNfse,
    DateTime? DataEmissao,
    string? DanfseRef,
    string? CodigoErro,
    string? MotivoErro);

public record NfseStatus(
    NfseSituacao Situacao,
    string? NumeroNfse,
    DateTime? DataEmissao,
    string? DanfseRef,
    string? CodigoErro,
    string? MotivoErro);

public enum NfseSituacao
{
    Autorizada,
    Cancelada,
    Rejeitada,
    Processando,
    NaoEncontrada,
}
