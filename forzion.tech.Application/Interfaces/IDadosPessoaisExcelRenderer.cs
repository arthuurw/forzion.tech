using forzion.tech.Application.UseCases.Conta.Lgpd;

namespace forzion.tech.Application.Interfaces;

public interface IDadosPessoaisExcelRenderer
{
    byte[] Render(DadosPessoaisExport dados);
}
