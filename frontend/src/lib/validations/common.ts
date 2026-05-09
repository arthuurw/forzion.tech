import { z } from "zod";

export const emailSchema = z
  .string()
  .min(1, "E-mail obrigatório")
  .email("E-mail inválido");

export const passwordSchema = z
  .string()
  .min(8, "Mínimo 8 caracteres");

export const nomeSchema = z
  .string()
  .min(2, "Nome deve ter ao menos 2 caracteres")
  .max(100, "Nome muito longo");

export const telefoneSchema = z
  .string()
  .regex(/^\d{10,11}$/, "Telefone inválido (somente dígitos, 10 ou 11)")
  .optional()
  .or(z.literal(""));

export const loginSchema = z.object({
  email: emailSchema,
  password: passwordSchema,
});
export type LoginFormData = z.infer<typeof loginSchema>;

export const cadastroTreinadorSchema = z
  .object({
    nome: nomeSchema,
    email: emailSchema,
    password: passwordSchema,
    confirmPassword: z.string().min(1, "Confirmação obrigatória"),
  })
  .refine((d) => d.password === d.confirmPassword, {
    message: "As senhas não coincidem",
    path: ["confirmPassword"],
  });
export type CadastroTreinadorFormData = z.infer<typeof cadastroTreinadorSchema>;

export const cadastroAlunoSchema = z
  .object({
    nome: nomeSchema,
    email: emailSchema,
    telefone: z
      .string()
      .regex(/^\d{10,11}$/, "Telefone inválido (somente dígitos, 10 ou 11)"),
    password: passwordSchema,
    confirmPassword: z.string().min(1, "Confirmação obrigatória"),
    diasDisponiveis: z.string().min(1, "Selecione os dias disponíveis"),
    tempoDisponivelMinutos: z.string().min(1, "Selecione o tempo disponível"),
    finalidade: z.string().min(1, "Selecione a finalidade do treino"),
    nivelCondicionamento: z.string().min(1, "Selecione o nível de condicionamento"),
    focoTreino: z.string().max(200, "Máximo 200 caracteres").optional().or(z.literal("")),
    limitacoesFisicas: z.string().max(500, "Máximo 500 caracteres").optional().or(z.literal("")),
    doencas: z.string().max(500, "Máximo 500 caracteres").optional().or(z.literal("")),
    observacoesAdicionais: z.string().max(1000, "Máximo 1000 caracteres").optional().or(z.literal("")),
  })
  .refine((d) => d.password === d.confirmPassword, {
    message: "As senhas não coincidem",
    path: ["confirmPassword"],
  });
export type CadastroAlunoFormData = z.infer<typeof cadastroAlunoSchema>;
