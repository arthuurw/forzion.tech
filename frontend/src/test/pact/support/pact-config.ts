import path from "node:path";

/**
 * Nomes canonicos do par consumer/provider e diretorio de saida dos contratos.
 *
 * O frontend (este pacote) e o CONSUMER: gera o contrato a partir das
 * requisicoes que o `apiClient` realmente envia. O backend .NET e o PROVIDER:
 * verifica o contrato publicado no broker (job separado, fora deste pacote).
 *
 * Mantido em um unico lugar para que os testes, o `pact-broker.config.ts` e o
 * workflow `contract.yml` nao divirjam de nomes.
 */
export const CONSUMER = "forzion-frontend";
export const PROVIDER = "forzion-api";

/** Diretorio onde os pacts gerados sao escritos (gitignored, publicado em CI). */
export const PACT_DIR = path.resolve(process.cwd(), "pacts");
