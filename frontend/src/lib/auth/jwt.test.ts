import { describe, it, expect } from "vitest";
import { parseJwtPayload, extractTipoConta } from "./jwt";

function makeJwt(payload: Record<string, unknown>): string {
  const header = btoa(JSON.stringify({ alg: "HS256", typ: "JWT" }));
  const body = btoa(JSON.stringify(payload));
  return `${header}.${body}.fakesig`;
}

describe("parseJwtPayload", () => {
  it("decodifica payload válido", () => {
    const token = makeJwt({ conta_id: "c1", tipo_conta: "Aluno" });
    expect(parseJwtPayload(token)).toMatchObject({ conta_id: "c1", tipo_conta: "Aluno" });
  });

  it("decodifica base64url (chars - e _)", () => {
    // Payload cujo base64 contém '+' e '/', forçando o replace url-safe.
    const raw = JSON.stringify({ tipo_conta: "Treinador", n: "??>>>>" });
    const urlSafe = btoa(raw).replace(/\+/g, "-").replace(/\//g, "_");
    const token = `aaa.${urlSafe}.bbb`;
    expect(parseJwtPayload(token)).toMatchObject({ tipo_conta: "Treinador" });
  });

  it("retorna null se não houver 3 partes", () => {
    expect(parseJwtPayload("a.b")).toBeNull();
  });

  it("retorna null quando o payload não é base64/JSON válido (catch)", () => {
    expect(parseJwtPayload("a.@@@invalid@@@.c")).toBeNull();
  });
});

describe("extractTipoConta — payload sem campo", () => {
  it("retorna null quando parse falha", () => {
    expect(extractTipoConta("a.@@@.c")).toBeNull();
  });
});
