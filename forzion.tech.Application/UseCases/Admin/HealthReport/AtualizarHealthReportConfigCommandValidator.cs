using FluentValidation;

namespace forzion.tech.Application.UseCases.Admin.HealthReport;

public class AtualizarHealthReportConfigCommandValidator : AbstractValidator<AtualizarHealthReportConfigCommand>
{
    public AtualizarHealthReportConfigCommandValidator()
    {
        RuleFor(x => x.Destinatarios)
            .NotEmpty().WithMessage("Informe ao menos um destinatário quando o relatório está ativo.")
            .When(x => x.Ativo);
    }
}
