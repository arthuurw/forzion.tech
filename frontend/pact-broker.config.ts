import { CONSUMER, PROVIDER } from "./src/test/pact/support/pact-config";

/**
 * Fonte unica de verdade para publicacao de contratos no Pact Broker.
 *
 * Nao roda em runtime do app — e consumido pelo workflow `contract.yml` (via
 * variaveis de ambiente) e documenta os parametros de publish/can-i-deploy.
 *
 * O broker so e exercido em CI quando `PACT_BROKER_BASE_URL` esta definido
 * (secret). Sem broker, os contratos ainda sao gerados e validados localmente
 * por `npm run test:contract` — a publicacao e o gate `can-i-deploy` ativam
 * quando o broker (self-hosted Docker em homolog ou PactFlow free tier) existir.
 */
export interface PactBrokerConfig {
  /** Nome do consumer (este frontend). */
  consumer: string;
  /** Nome do provider (backend .NET). */
  provider: string;
  /** Diretorio relativo onde os pacts sao gerados. */
  pactDir: string;
  /** URL base do broker. Ausente => publish/can-i-deploy desativados. */
  brokerBaseUrl: string | undefined;
  /** Token de auth do broker (PactFlow) — alternativa a user/pass. */
  brokerToken: string | undefined;
  /** Versao do consumer publicada (SHA do commit). */
  consumerVersion: string | undefined;
  /** Branch usada como tag de ambiente no broker. */
  branch: string | undefined;
}

export const pactBrokerConfig: PactBrokerConfig = {
  consumer: CONSUMER,
  provider: PROVIDER,
  pactDir: "pacts",
  brokerBaseUrl: process.env.PACT_BROKER_BASE_URL,
  brokerToken: process.env.PACT_BROKER_TOKEN,
  consumerVersion: process.env.GITHUB_SHA ?? process.env.GIT_COMMIT,
  branch: process.env.GITHUB_REF_NAME ?? process.env.GIT_BRANCH,
};
