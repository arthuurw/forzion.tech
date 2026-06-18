import { describe, it, expect } from "vitest";
import { extractApiError, extractApiErrorInfo } from "./extractApiError";

/** Build a minimal axios-like error with response.data fields. */
function makeAxiosError(data: Record<string, unknown>): unknown {
  return { response: { data } };
}

describe("extractApiError", () => {
  it("returns detail when present (highest precedence)", () => {
    const err = makeAxiosError({ detail: "Pacote já está em uso.", title: "Bad Request", message: "error" });
    expect(extractApiError(err)).toBe("Pacote já está em uso.");
  });

  it("returns title when detail is absent", () => {
    const err = makeAxiosError({ title: "Validation failed", message: "some error" });
    expect(extractApiError(err)).toBe("Validation failed");
  });

  it("returns message when both detail and title are absent", () => {
    const err = makeAxiosError({ message: "Internal server error" });
    expect(extractApiError(err)).toBe("Internal server error");
  });

  it("returns custom fallback when no recognised field is present", () => {
    const err = makeAxiosError({ code: "SOME_CODE" });
    expect(extractApiError(err, "Erro ao excluir pacote.")).toBe("Erro ao excluir pacote.");
  });

  it("returns default generic fallback when no field matches and no custom fallback", () => {
    const err = makeAxiosError({});
    expect(extractApiError(err)).toBe("Ocorreu um erro. Tente novamente.");
  });

  it("returns fallback for non-axios error (plain Error)", () => {
    const err = new Error("Network Error");
    expect(extractApiError(err, "Erro de rede.")).toBe("Erro de rede.");
  });

  it("returns fallback for null error", () => {
    expect(extractApiError(null, "Fallback.")).toBe("Fallback.");
  });

  it("returns fallback for string error", () => {
    expect(extractApiError("oops", "Fallback string.")).toBe("Fallback string.");
  });

  it("ignores whitespace-only detail and falls through to title", () => {
    const err = makeAxiosError({ detail: "   ", title: "Something went wrong" });
    expect(extractApiError(err)).toBe("Something went wrong");
  });

  it("ignores whitespace-only title and falls through to message", () => {
    const err = makeAxiosError({ detail: "  ", title: "\t", message: "Bad gateway" });
    expect(extractApiError(err)).toBe("Bad gateway");
  });

  it("returns fallback when response.data is not an object", () => {
    const err = { response: { data: "plain string" } };
    expect(extractApiError(err, "Fallback plain.")).toBe("Fallback plain.");
  });

  it("returns fallback when response is absent", () => {
    const err = { message: "Network Error" };
    expect(extractApiError(err, "Sem resposta.")).toBe("Sem resposta.");
  });
});

describe("extractApiErrorInfo", () => {
  it("exposes the root-level code extension member", () => {
    const err = {
      response: { status: 409, data: { detail: "x", code: "assinatura_treinador.offboarding_necessario" } },
    };
    expect(extractApiErrorInfo(err)).toEqual({
      message: "x",
      code: "assinatura_treinador.offboarding_necessario",
      status: 409,
    });
  });

  it("exposes status even when data has no message", () => {
    const err = { response: { status: 404, data: {} } };
    expect(extractApiErrorInfo(err)).toEqual({ message: null, code: null, status: 404 });
  });

  it("applies detail → title → message precedence for message", () => {
    expect(extractApiErrorInfo({ response: { data: { title: "T", message: "M" } } }).message).toBe("T");
    expect(extractApiErrorInfo({ response: { data: { message: "M" } } }).message).toBe("M");
  });

  it("returns all-null for non-axios errors", () => {
    expect(extractApiErrorInfo(new Error("x"))).toEqual({ message: null, code: null, status: null });
    expect(extractApiErrorInfo(null)).toEqual({ message: null, code: null, status: null });
  });

  it("ignores whitespace-only code", () => {
    expect(extractApiErrorInfo({ response: { data: { code: "   " } } }).code).toBeNull();
  });
});
