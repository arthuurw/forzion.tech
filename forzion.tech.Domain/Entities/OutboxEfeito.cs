using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.Entities;

/// <summary>
/// Efeito externo pós-commit registrado de forma durável p/ entrega garantida com retry.
/// Gravado no MESMO commit do agregado de origem (atomicidade); processado por
/// OutboxProcessorService. <see cref="Tipo"/> discrimina o handler: "evt:&lt;CLR&gt;"
/// (re-dispatch de domain-event) ou "fx:&lt;nome&gt;" (efeito nomeado).
/// </summary>
public class OutboxEfeito
{
    public Guid Id { get; private set; }
    public string Tipo { get; private set; } = null!;
    public string Payload { get; private set; } = null!;
    public OutboxStatus Status { get; private set; }
    public int Tentativas { get; private set; }
    public DateTime ProximaTentativa { get; private set; }
    public string? UltimoErro { get; private set; }
    public string ChaveIdempotencia { get; private set; } = null!;
    public DateTime CriadoEm { get; private set; }
    public DateTime? ProcessadoEm { get; private set; }

    private OutboxEfeito() { }

    public static Result<OutboxEfeito> Criar(string tipo, string payload, string chaveIdempotencia, DateTime agora)
    {
        if (string.IsNullOrWhiteSpace(tipo))
            return Result.Failure<OutboxEfeito>(OutboxErrors.TipoObrigatorio);
        if (string.IsNullOrWhiteSpace(payload))
            return Result.Failure<OutboxEfeito>(OutboxErrors.PayloadObrigatorio);
        if (string.IsNullOrWhiteSpace(chaveIdempotencia))
            return Result.Failure<OutboxEfeito>(OutboxErrors.ChaveIdempotenciaObrigatoria);

        return Result.Success(new OutboxEfeito
        {
            Id = Guid.NewGuid(),
            Tipo = tipo,
            Payload = payload,
            ChaveIdempotencia = chaveIdempotencia,
            Status = OutboxStatus.Pendente,
            Tentativas = 0,
            // Processável imediatamente: worker pega no próximo scan.
            ProximaTentativa = agora,
            CriadoEm = agora
        });
    }

    public void MarcarProcessando()
    {
        GarantirStatus(OutboxStatus.Pendente);
        Status = OutboxStatus.Processando;
    }

    public void MarcarConcluido(DateTime agora)
    {
        GarantirStatus(OutboxStatus.Processando);
        Status = OutboxStatus.Concluido;
        ProcessadoEm = agora;
    }

    // Falha transiente: volta a Pendente p/ novo scan após o backoff (proximaTentativa calculada pelo worker).
    public void RegistrarFalha(string erro, DateTime proximaTentativa)
    {
        GarantirStatus(OutboxStatus.Processando);
        Tentativas++;
        UltimoErro = Truncar(erro);
        ProximaTentativa = proximaTentativa;
        Status = OutboxStatus.Pendente;
    }

    // Esgotou a política de retry: estado terminal auditável (worker loga Critical).
    public void MarcarFalhouDefinitivo(string erro, DateTime agora)
    {
        GarantirStatus(OutboxStatus.Processando);
        Tentativas++;
        UltimoErro = Truncar(erro);
        Status = OutboxStatus.Falhou;
        ProcessadoEm = agora;
    }

    private void GarantirStatus(OutboxStatus esperado)
    {
        // Invariante de máquina de estado: worker sempre lê (Processando) antes de concluir/falhar.
        // Violação = bug de orquestração, não erro de negócio.
        if (Status != esperado)
            throw new InvalidOperationException($"Transição inválida do outbox: esperado {esperado}, atual {Status}.");
    }

    // Erro pode carregar stack/detalhe longo; limita p/ não inchar a linha de auditoria.
    private static string Truncar(string erro) =>
        string.IsNullOrEmpty(erro) || erro.Length <= 2000 ? erro : erro[..2000];
}
