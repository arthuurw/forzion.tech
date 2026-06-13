namespace forzion.tech.Application.UseCases.Suporte.EnviarMensagem;

// Identidade do remetente NÃO entra aqui: é resolvida do contexto autenticado no handler
// (anti-spoofing). O cliente só informa categoria/assunto/descrição.
public record EnviarMensagemSuporteCommand(string Categoria, string Assunto, string Descricao);
