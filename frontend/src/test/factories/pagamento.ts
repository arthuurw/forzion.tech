import { faker } from "@faker-js/faker";
import type {
  PagamentoResponse,
  PagamentoStatus,
  MetodoPagamento,
  AssinaturaResponse,
  AssinaturaStatus,
} from "@/types";

export function buildPagamento(
  overrides: Partial<PagamentoResponse> = {},
): PagamentoResponse {
  const status: PagamentoStatus = "Pago";
  const metodoPagamento: MetodoPagamento = "Cartao";
  return {
    pagamentoId: faker.string.uuid(),
    assinaturaId: faker.string.uuid(),
    valor: faker.number.float({ min: 50, max: 500, fractionDigits: 2 }),
    status,
    metodoPagamento,
    pixQrCode: null,
    pixQrCodeUrl: null,
    pixExpiracao: null,
    clientSecret: null,
    dataPagamento: faker.date.recent({ days: 7 }).toISOString(),
    createdAt: faker.date.recent({ days: 7 }).toISOString(),
    ...overrides,
  };
}

export function buildAssinatura(
  overrides: Partial<AssinaturaResponse> = {},
): AssinaturaResponse {
  const status: AssinaturaStatus = "Ativa";
  return {
    assinaturaId: faker.string.uuid(),
    vinculoId: faker.string.uuid(),
    pacoteAlunoId: faker.string.uuid(),
    treinadorId: faker.string.uuid(),
    alunoId: faker.string.uuid(),
    valor: faker.number.float({ min: 50, max: 500, fractionDigits: 2 }),
    status,
    dataInicio: faker.date.recent({ days: 30 }).toISOString(),
    dataProximaCobranca: faker.date.future({ years: 0.1 }).toISOString(),
    dataCancelamento: null,
    createdAt: faker.date.recent({ days: 30 }).toISOString(),
    ...overrides,
  };
}
