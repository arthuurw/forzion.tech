import { faker } from "@faker-js/faker";
import type {
  PagamentoResponse,
  PagamentoStatus,
  MetodoPagamento,
  AssinaturaAlunoResponse,
  AssinaturaAlunoStatus,
} from "@/types";

export function buildPagamento(
  overrides: Partial<PagamentoResponse> = {},
): PagamentoResponse {
  const status: PagamentoStatus = "Pago";
  const metodoPagamento: MetodoPagamento = "Cartao";
  return {
    pagamentoId: faker.string.uuid(),
    assinaturaAlunoId: faker.string.uuid(),
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

export function buildAssinaturaAluno(
  overrides: Partial<AssinaturaAlunoResponse> = {},
): AssinaturaAlunoResponse {
  const status: AssinaturaAlunoStatus = "Ativa";
  return {
    assinaturaAlunoId: faker.string.uuid(),
    vinculoId: faker.string.uuid(),
    pacoteId: faker.string.uuid(),
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
