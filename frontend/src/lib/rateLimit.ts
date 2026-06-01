/**
 * Rate limiter in-memory para o proxy /api/auth/* do frontend.
 *
 * LIMITAÇÕES CONHECIDAS (cobertas por defesa em profundidade no backend):
 *   1. Estado vive em Map per-process. Com `next start` em N réplicas, o cap
 *      efetivo vira `RATE_LIMIT × N`. O backend tem rate limiter próprio
 *      (particionado por IP/sub) que é a defesa autoritativa.
 *   2. Sem store compartilhada (Redis/Upstash). Migrar quando escalar > 1 réplica.
 *
 * Map é bounded em MAX_ENTRIES para evitar crescimento ilimitado por flood
 * de IPs distintos. Eviction LRU por resetAt expirado.
 */

const RATE_LIMIT = 10;
const WINDOW_MS = 60_000;
const MAX_ENTRIES = 10_000;

const loginAttempts = new Map<string, { count: number; resetAt: number }>();

function pruneExpired(now: number): void {
  if (loginAttempts.size < MAX_ENTRIES) return;
  for (const [k, v] of loginAttempts) {
    if (now > v.resetAt) loginAttempts.delete(k);
  }
  // Se ainda estourou MAX_ENTRIES após podar expirados (ataque ativo), descarta
  // entradas mais antigas pela ordem de inserção do Map.
  if (loginAttempts.size >= MAX_ENTRIES) {
    const excess = loginAttempts.size - MAX_ENTRIES + 1;
    let dropped = 0;
    for (const k of loginAttempts.keys()) {
      loginAttempts.delete(k);
      if (++dropped >= excess) break;
    }
  }
}

export function checkRateLimit(ip: string): boolean {
  const now = Date.now();
  pruneExpired(now);
  const entry = loginAttempts.get(ip);
  if (!entry || now > entry.resetAt) {
    loginAttempts.set(ip, { count: 1, resetAt: now + WINDOW_MS });
    return true;
  }
  if (entry.count >= RATE_LIMIT) return false;
  entry.count++;
  return true;
}

/**
 * Resolve o IP de origem na ordem:
 *   1. `X-Real-IP` (nginx injeta — fonte mais confiável atrás de proxy próprio).
 *   2. Primeiro hop de `X-Forwarded-For` (RFC 7239: cliente original).
 *
 * NUNCA usar o último hop de XFF: esse é o proxy mais próximo (ex.: nginx) e
 * permite spoofing trivial — o cliente envia "10.0.0.1, evil" e o último vira
 * "evil", colidindo entradas distintas no mesmo bucket.
 */
export function getClientIp(request: { headers: { get(name: string): string | null } }): string {
  const realIp = request.headers.get("x-real-ip");
  if (realIp) return realIp.trim();
  const xff = request.headers.get("x-forwarded-for");
  if (xff) {
    const first = xff.split(",")[0]?.trim();
    if (first) return first;
  }
  return "unknown";
}

// Helper exposto para testes: limpa estado entre cenários.
export function __resetRateLimit(): void {
  loginAttempts.clear();
}
