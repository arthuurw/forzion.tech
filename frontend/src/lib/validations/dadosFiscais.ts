import { z } from "zod";

export const soDigitos = (v: string) => v.replace(/\D/g, "");

export function mascararCpf(v: string): string {
  const d = soDigitos(v).slice(0, 11);
  return d
    .replace(/^(\d{3})(\d)/, "$1.$2")
    .replace(/^(\d{3})\.(\d{3})(\d)/, "$1.$2.$3")
    .replace(/\.(\d{3})(\d)/, ".$1-$2");
}

export function mascararCnpj(v: string): string {
  const d = soDigitos(v).slice(0, 14);
  return d
    .replace(/^(\d{2})(\d)/, "$1.$2")
    .replace(/^(\d{2})\.(\d{3})(\d)/, "$1.$2.$3")
    .replace(/\.(\d{3})(\d)/, ".$1/$2")
    .replace(/(\d{4})(\d)/, "$1-$2");
}

export function mascararDocumento(tipo: "Cpf" | "Cnpj", v: string): string {
  return tipo === "Cpf" ? mascararCpf(v) : mascararCnpj(v);
}

export function mascararCep(v: string): string {
  return soDigitos(v).slice(0, 8).replace(/^(\d{5})(\d)/, "$1-$2");
}

export const dadosFiscaisSchema = z
  .object({
    tipoDocumento: z.enum(["Cpf", "Cnpj"]),
    documento: z.string().min(1, "Documento obrigatório"),
    razaoSocial: z.string().trim().min(2, "Nome/Razão social obrigatório").max(150, "Máximo 150 caracteres"),
    inscricaoMunicipal: z.string().trim().max(30, "Máximo 30 caracteres").optional().or(z.literal("")),
    logradouro: z.string().trim().min(2, "Logradouro obrigatório").max(150, "Máximo 150 caracteres"),
    numero: z.string().trim().min(1, "Número obrigatório").max(20, "Máximo 20 caracteres"),
    complemento: z.string().trim().max(60, "Máximo 60 caracteres").optional().or(z.literal("")),
    bairro: z.string().trim().min(2, "Bairro obrigatório").max(80, "Máximo 80 caracteres"),
    codigoMunicipioIbge: z.string().regex(/^\d{7}$/, "Código IBGE deve ter 7 dígitos"),
    uf: z.string().trim().length(2, "UF deve ter 2 letras"),
    cep: z.string().refine((v) => soDigitos(v).length === 8, "CEP deve ter 8 dígitos"),
  })
  .refine(
    (d) => soDigitos(d.documento).length === (d.tipoDocumento === "Cpf" ? 11 : 14),
    { path: ["documento"], message: "Documento inválido para o tipo selecionado" },
  );

export type DadosFiscaisFormData = z.infer<typeof dadosFiscaisSchema>;
