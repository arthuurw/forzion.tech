// k6: prova de isolamento de latência de provider (AC-2.4 / PERF-04). Martela o
// request-path quente (login no setup, GET dashboard, GET execuções) MAIS a ação que
// enfileira um efeito DURÁVEL de provider — POST /suporte/mensagens, cujo e-mail ao
// suporte é despachado via OUTBOX (MensagemSuporteCriadaEvent registrado durável), FORA
// do request-path. Roda 2×: provider de e-mail com latência 0 vs ALTA (toxiproxy mail).
// Se o p95 do POST /suporte/mensagens NÃO inflar com a latência do provider, a chamada
// externa está diferida (isolamento CONFIRMADO). Se inflar ~latência → chamada SÍNCRONA
// no request-path = vazamento PERF-04.
//
//   k6 run -e BASE=http://localhost:5080 -e LABEL=lat0 scripts/perf/load/k6-provider-isolation.js
import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend } from 'k6/metrics';

const BASE = __ENV.BASE || 'http://localhost:5080';
const SENHA = __ENV.ALUNO_SENHA || 'Bench@123456';
const VUS = Number(__ENV.VUS || 8);
const DUR = __ENV.DUR || '60s';

const dashLat = new Trend('dashboard_latencia', true);
const histLat = new Trend('execucoes_latencia', true);
const suporteLat = new Trend('suporte_post_latencia', true);

export const options = {
  setupTimeout: '60s',
  scenarios: {
    request_path: { executor: 'constant-vus', vus: VUS, duration: DUR },
  },
  thresholds: {
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

  const s = http.post(`${BASE}/suporte/mensagens`, JSON.stringify({
    categoria: 'Duvida',
    assunto: `carga isolamento provider vu${__VU}`,
    descricao: 'Mensagem sintetica de load test para enfileirar efeito duravel de provider via outbox.',
  }), auth);
  suporteLat.add(s.timings.duration);
  check(s, { 'suporte 202': (r) => r.status === 202 });

  sleep(2);
}
