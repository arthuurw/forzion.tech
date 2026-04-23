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
    telefone: telefoneSchema,
    password: passwordSchema,
    confirmPassword: z.string().min(1, "Confirmação obrigatória"),
  })
  .refine((d) => d.password === d.confirmPassword, {
    message: "As senhas não coincidem",
    path: ["confirmPassword"],
  });
export type CadastroAlunoFormData = z.infer<typeof cadastroAlunoSchema>;
