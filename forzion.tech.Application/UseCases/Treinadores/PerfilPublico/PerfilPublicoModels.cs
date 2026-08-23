namespace forzion.tech.Application.UseCases.Treinadores.PerfilPublico;

public record EnderecoPublicoInput(
    string Rua,
    string? Numero,
    string? Complemento,
    string Bairro,
    string Cidade,
    string Estado,
    string Cep);

public record HorarioFuncionamentoInput(int DiaSemana, TimeOnly AbreAs, TimeOnly FechaAs);

public record DefinirPerfilPublicoTreinadorCommand(
    Guid TreinadorId,
    string? NomeFantasia,
    EnderecoPublicoInput? Endereco,
    IReadOnlyDictionary<string, string>? Politicas,
    IReadOnlyList<HorarioFuncionamentoInput> Horarios,
    bool IsPublicado,
    string FusoHorario);

public record EnderecoPublicoResponse(
    string Rua,
    string? Numero,
    string? Complemento,
    string Bairro,
    string Cidade,
    string Estado,
    string Cep);

public record HorarioFuncionamentoResponse(Guid Id, int DiaSemana, string AbreAs, string FechaAs);

public record PerfilPublicoResponse(
    string? NomeFantasia,
    EnderecoPublicoResponse? Endereco,
    IReadOnlyList<HorarioFuncionamentoResponse> HorariosFuncionamento,
    IReadOnlyDictionary<string, string>? Politicas,
    bool IsPublicado,
    string FusoHorario);
