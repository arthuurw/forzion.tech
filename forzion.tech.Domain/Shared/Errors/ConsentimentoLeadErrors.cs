namespace forzion.tech.Domain.Shared.Errors;

public static class ConsentimentoLeadErrors
{
    public static Error FinalidadeObrigatoria => Error.Validation("consentimento_lead.finalidade_obrigatoria", "A finalidade do consentimento é obrigatória.");
    public static Error FinalidadeMuitoLonga => Error.Validation("consentimento_lead.finalidade_muito_longa", "A finalidade do consentimento deve ter no máximo 500 caracteres.");
}
