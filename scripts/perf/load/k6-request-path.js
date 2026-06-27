// k6: request-path quente do aluno sob carga — dashboard + histórico de execuções.
// Cada VU loga como um aluno distinto (aluno1..alunoN@bench.local, senha fixa) para
// espalhar o rate-limit "read"/"write" (por sub) e o "auth" (por IP, cap 10/min → manter
// VUS ≤ 8). Rodar concorrente ao k6-batch-trigger.js p/ medir starvation de pool sob o lote.
//
//   k6 run -e BASE=http://localhost:5080 -e VUS=8 scripts/perf/load/k6-request-path.js
import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend } from 'k6/metrics';

const BASE = __ENV.BASE || 'http://localhost:5080';
const SENHA = __ENV.ALUNO_SENHA || 'Bench@123456';
const VUS = Number(__ENV.VUS || 8);
const DUR = __ENV.DUR || '100s';

const dashLat = new Trend('dashboard_latencia', true);
const histLat = new Trend('execucoes_latencia', true);

export const options = {
  scenarios: {
    request_path: { executor: 'constant-vus', vus: VUS, duration: DUR },
  },
  thresholds: {
    dashboard_latencia: ['p(95)<800'],
    execucoes_latencia: ['p(95)<800'],
    http_req_failed: ['rate<0.01'],
  },
};

export function setup() {
  const tokens = {};
  for (let v = 1; v <= VUS; v++) {
    const email = `aluno${v}@bench.local`;
    const res = http.post(`${BASE}/auth/login`, JSON.stringify({ email, senha: SENHA }),
      { headers: { 'Content-Type': 'application/json' } });
    check(res, { [`login ${email} 200`]: (r) => r.status === 200 });
    tokens[v] = res.json('token');
  }
  return { tokens };
}

export default function (data) {
  const token = data.tokens[__VU] || data.tokens[1];
  const auth = { headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' } };

  const d = http.get(`${BASE}/aluno/dashboard`, auth);
  dashLat.add(d.timings.duration);
  check(d, { 'dashboard ok': (r) => r.status === 200 });

  const h = http.get(`${BASE}/aluno/execucoes?pagina=1&tamanhoPagina=20`, auth);
  histLat.add(h.timings.duration);
  check(h, { 'execucoes ok': (r) => r.status === 200 });

  sleep(2);
}
