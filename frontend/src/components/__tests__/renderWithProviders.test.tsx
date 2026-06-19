import { describe, it, expect } from "vitest";
import { screen } from "@testing-library/react";
import { renderWithProviders } from "@/test/render";
import { buildSessionUser, buildConta } from "@/test/factories";

describe("renderWithProviders skip flags + session factories", () => {
  it("renders with skipAuth without mounting the auth provider", () => {
    const user = buildSessionUser({ tipoConta: "Treinador" });
    renderWithProviders(<div>{`conta:${user.contaId}`}</div>, {
      skipAuth: true,
    });
    expect(screen.getByText(`conta:${user.contaId}`)).toBeInTheDocument();
  });

  it("buildConta produces a full LoginResponse payload", () => {
    const conta = buildConta({ tipoConta: "SystemAdmin" });
    expect(conta.tipoConta).toBe("SystemAdmin");
    expect(conta.token).toBeTruthy();
    expect(conta.contaId).toBeTruthy();
    expect(conta.perfilId).toBeTruthy();
  });
});
