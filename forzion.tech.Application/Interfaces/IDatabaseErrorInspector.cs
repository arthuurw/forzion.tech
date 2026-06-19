namespace forzion.tech.Application.Interfaces;

// Traduz exceções do provider de persistência sem vazar o tipo concreto (Npgsql/EF) para a
// Application. Detecção por código de erro estável (SqlState), não por substring de mensagem
// — esta quebra com mudança de wording/locale do driver.
public interface IDatabaseErrorInspector
{
    bool EhViolacaoDeUnicidade(Exception exception);

    bool EhConflitoDeSerializacao(Exception exception);
}
