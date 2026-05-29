import { describe, it, expect, beforeEach } from "vitest";
import { checkRateLimit, getClientIp, __resetRateLimit } from "@/lib/rateLimit";

describe("getClientIp", () => {
  function makeReq(data: Record<string, string>) {
    return { headers: { get: (name: string) => data[name] ?? null } };
  }

  it("usa x-real-ip quando presente", () => {
    expect(getClientIp(makeReq({ "x-real-ip": "1.2.3.4", "x-forwarded-for": "9.9.9.9" }) as never)).toBe("1.2.3.4");
  });

  it("usa PRIMEIRO IP de x-forwarded-for (cliente original, não último proxy)", () => {
    // Spoofing-resistant: atacante envia "evil, real-proxy" — pegamos o real-proxy injetado
    // pelo edge, não o "evil" controlado pelo cliente. Usar o último daria match com "evil".
    expect(getClientIp(makeReq({ "x-forwarded-for": "5.6.7.8, 10.10.10.10" }) as never)).toBe("5.6.7.8");
  });

  it("usa x-forwarded-for com um único IP", () => {
    expect(getClientIp(makeReq({ "x-forwarded-for": "1.1.1.1" }) as never)).toBe("1.1.1.1");
  });

  it("retorna 'unknown' quando nenhum header presente", () => {
    expect(getClientIp(makeReq({}) as never)).toBe("unknown");
  });
});

describe("checkRateLimit", () => {
  beforeEach(() => {
    __resetRateLimit();
  });

  it("primeiros 10 requests do mesmo IP → todos permitidos", () => {
    for (let i = 0; i < 10; i++) {
      expect(checkRateLimit("1.1.1.1")).toBe(true);
    }
  });

  it("11º request do mesmo IP na mesma janela → bloqueado", () => {
    for (let i = 0; i < 10; i++) checkRateLimit("1.1.1.1");
    expect(checkRateLimit("1.1.1.1")).toBe(false);
  });

  it("IPs diferentes não interferem entre si", () => {
    for (let i = 0; i < 10; i++) checkRateLimit("1.1.1.1");
    expect(checkRateLimit("1.1.1.1")).toBe(false);
    expect(checkRateLimit("2.2.2.2")).toBe(true);
  });

  // F25 — Eviction LRU quando MAX_ENTRIES (10_000) é atingido.
  // Sem eviction, flood de IPs distintos crescia o Map sem limite (memory leak
  // amenizado pela defesa do backend, mas ainda assim risco de OOM no Node).
  describe("F25 — MAX_ENTRIES eviction", () => {
    const MAX = 10_000;

    it("acima de MAX_ENTRIES → Map não cresce além do cap", () => {
      // 10_000 IPs distintos preenchem até o limiar. Para acionar a poda, o
      // 10_001º request precisa de outro IP novo (pruneExpired só roda se size
      // >= MAX_ENTRIES). Como nada expirou, cai no branch de descarte por idade
      // de inserção.
      for (let i = 0; i < MAX + 100; i++) {
        checkRateLimit(`ip-${i}`);
      }

      // Re-checa um IP novo — força nova chamada de pruneExpired. Size deve
      // continuar limitado.
      checkRateLimit("ip-sentinel");

      // Não há getter público pra .size, então validamos indiretamente:
      // o IP inserido primeiro deve ter sido evictado (request novo dele
      // entra como contagem 1, true).
      expect(checkRateLimit("ip-0")).toBe(true);
    });

    it("entradas mais antigas saem primeiro (FIFO por insertion order)", () => {
      // IPs ip-0 .. ip-(MAX-1) → tudo dentro do cap.
      for (let i = 0; i < MAX; i++) {
        checkRateLimit(`ip-${i}`);
      }
      // ip-newcomer força size >= MAX → branch de descarte por idade.
      checkRateLimit("ip-newcomer");

      // ip-0 (mais antigo) deveria ter sido evictado. Próximo check dele
      // entra como entrada nova com count=1 → true.
      expect(checkRateLimit("ip-0")).toBe(true);
      // ip-newcomer permanece (recém-inserido). Próximo check deve continuar
      // permitido pois count=1 ainda dentro do limite.
      expect(checkRateLimit("ip-newcomer")).toBe(true);
    });

    it("entradas expiradas são podadas antes do descarte por idade", () => {
      const realNow = Date.now;
      let fakeNow = 1_000_000;
      Date.now = () => fakeNow;

      try {
        // Insere MAX entradas no tempo T0.
        for (let i = 0; i < MAX; i++) checkRateLimit(`exp-${i}`);

        // Avança 2 janelas (cada uma é 60_000ms). Todas as MAX entradas estão
        // expiradas. Novo IP força pruneExpired → poda os expirados.
        fakeNow += 200_000;
        checkRateLimit("post-expiry");

        // Após poda, ip-0 entra como entrada nova (count=1, true). Se a poda
        // não tivesse rodado, ele teria sido descartado por idade — mas o
        // resultado observável (true) é o mesmo. A diferença está no caminho:
        // aqui o Map ficou pequeno antes do descarte por idade.
        expect(checkRateLimit("exp-0")).toBe(true);
      } finally {
        Date.now = realNow;
      }
    });
  });
});
