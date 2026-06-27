// k6: request-path quente do aluno sob carga — login + dashboard + histórico + POST execução.
// RUNBOOK (não roda sozinho): exige a API no ar apontando ao schema do seed e contas
// logáveis (hash bcrypt real — ver scripts/perf/load/README na resultados-load.md).
// Rodar junto do k6-batch-trigger.js (lote) p/ reproduzir starvation de pool.
//
//   BASE=http://localhost:5000 ALUNO_EMAIL=aluno1@bench.local ALUNO_SENHA=... \
//     k6 run -e BASE=$BASE -e ALUNO_EMAIL=$ALUNO_EMAIL -e ALUNO_SENHA=$ALUNO_SENHA \
//     scripts/perf/load/k6-request-path.js
import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend } from 'k6/metrics';

const BASE = __ENV.BASE || 'http://localhost:5000';
const EMAIL = __ENV.ALUNO_EMAIL || 'aluno1@bench.local';
const SENHA = __ENV.ALUNO_SENHA || 'Bench@123456';

const dashLat = new Trend('dashboard_latencia', true);
const histLat = new Trend('historico_latencia', true);

export const options = {
  scenarios: {
    request_path: { executor: 'ramping-vus', startVUs: 0,
      stages: [ { duration: '20s', target: 50 }, { duration: '60s', target: 50 }, { duration: '10s', target: 0 } ] },
  },
  thresholds: {
    dashboard_latencia: ['p(95)<800'],
    historico_latencia: ['p(95)<800'],
    http_req_failed: ['rate<0.01'],
  },
};

export function setup() {
  const res = http.post(`${BASE}/auth/login`, JSON.stringify({ email: EMAIL, senha: SENHA }),
    { headers: { 'Content-Type': 'application/json' } });
  check(res, { 'login 200': (r) => r.status === 200 });
  return { token: res.json('accessToken') };
}

export default function (data) {
  const auth = { headers: { Authorization: `Bearer ${data.token}`, 'Content-Type': 'application/json' } };

  const d = http.get(`${BASE}/aluno/dashboard`, auth);
  dashLat.add(d.timings.duration);
  check(d, { 'dashboard ok': (r) => r.status === 200 });

  const h = http.get(`${BASE}/aluno/historico?pagina=1&tamanhoPagina=20`, auth);
  histLat.add(h.timings.duration);
  check(h, { 'historico ok': (r) => r.status === 200 });

  sleep(1);
}
