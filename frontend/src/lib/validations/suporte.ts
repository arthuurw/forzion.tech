import { z } from "zod";

// Limites espelham as invariantes do backend (MensagemSuporte.Criar + validator). Trim antes do
// min/max para casar com o backend, que valida o texto já trimado.
export const CATEGORIAS_SUPORTE = ["Duvida", "Sugestao", "Outro"] as const;

export const CATEGORIA_LABEL: Record<(typeof CATEGORIAS_SUPORTE)[number], string> = {
  Duvida: "Dúvida",
  Sugestao: "Sugestão",
  Outro: "Outro",
};

export const suporteSchema = z.object({
  categoria: z.enum(CATEGORIAS_SUPORTE, { message: "Selecione uma categoria" }),
  assunto: z
    .string()
    .trim()
    .min(3, "Assunto deve ter ao menos 3 caracteres")
    .max(120, "Assunto deve ter no máximo 120 caracteres"),
  descricao: z
    .string()
    .trim()
    .min(20, "Descrição deve ter ao menos 20 caracteres")
    .max(2000, "Descrição deve ter no máximo 2000 caracteres"),
});

export type SuporteFormData = z.infer<typeof suporteSchema>;
