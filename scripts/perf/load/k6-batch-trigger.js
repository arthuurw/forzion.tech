// k6: dispara o lote de pré-aviso de renovação concorrente ao request-path
// (k6-request-path.js). Cada disparo gera fan-out de domain-events best-effort
// (CobrancaProximaAlunoEvent → CobrancaProximaEmailAlunoHandler), 1 por assinatura
// na janela [+3d,+4d). Em homolog o dispatch é gated em 8 (BestEffortConcurrencyGate);
// com DomainEvents:MaxConcorrenciaBestEffort alto o fan-out vira ~unbounded e consome
// o pool sob o request-path. Rate baixo (≤5/min) por causa do rate-limit "internal".
// RUNBOOK: exige X-Internal-Key configurada na API + assinaturas na janela (seed patch).
//
//   k6 run -e BASE=http://localhost:5080 -e INTERNAL_KEY=... scripts/perf/load/k6-batch-trigger.js
import http from 'k6/http';
import { check } from 'k6';

const BASE = __ENV.BASE || 'http://localhost:5080';
const KEY = __ENV.INTERNAL_KEY || 'bench-internal-key';

export const options = {
  scenarios: {
    batch: { executor: 'constant-arrival-rate', rate: 1, timeUnit: '30s',
      duration: '100s', preAllocatedVUs: 2, maxVUs: 4,
      startTime: __ENV.START_DELAY || '0s' },
  },
};

export default function () {
  const res = http.post(`${BASE}/internal/processar-pre-avisos`, null,
    { headers: { 'X-Internal-Key': KEY } });
  check(res, { 'batch aceito': (r) => r.status === 200 || r.status === 503 });
  console.log(`batch -> ${res.status} ${res.body}`);
}
