// k6: dispara o lote de pré-aviso de renovação repetidamente, concorrente ao
// request-path (k6-request-path.js). Cada disparo gera fan-out de domain-events
// best-effort; em homolog o dispatch é UNBOUNDED (Task.Run sem gate — ver
// DomainEventDispatcher.cs:62) → consome slots de pool sob o request-path.
// Na perf/auditoria-performance o BestEffortConcurrencyGate (bound 8) limita.
// RUNBOOK: exige X-Internal-Key configurada na API.
//
//   k6 run -e BASE=http://localhost:5000 -e INTERNAL_KEY=... scripts/perf/load/k6-batch-trigger.js
import http from 'k6/http';
import { check } from 'k6';

const BASE = __ENV.BASE || 'http://localhost:5000';
const KEY = __ENV.INTERNAL_KEY || 'dev-internal-key';

export const options = {
  scenarios: {
    batch: { executor: 'constant-arrival-rate', rate: 2, timeUnit: '1s',
      duration: '90s', preAllocatedVUs: 5, maxVUs: 20 },
  },
};

export default function () {
  const res = http.post(`${BASE}/internal/processar-pre-avisos`, null,
    { headers: { 'X-Internal-Key': KEY } });
  check(res, { 'batch aceito': (r) => r.status === 200 || r.status === 503 });
}
