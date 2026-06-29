import { render } from "@testing-library/react";
import { axe } from "vitest-axe";
import { describe, it, expect } from "vitest";
import AlertBanner from "./AlertBanner";

describe("AlertBanner a11y", () => {
  it("severity error com title", async () => {
    const { container } = render(
      <AlertBanner open severity="error" title="Erro" message="Falhou ao salvar." />,
    );
    expect(await axe(container)).toHaveNoViolations();
  });

  it("severity success sem title", async () => {
    const { container } = render(
      <AlertBanner open severity="success" message="Salvo com sucesso." />,
    );
    expect(await axe(container)).toHaveNoViolations();
  });

  it("severity warning + onClose (botao close acessivel)", async () => {
    const { container } = render(
      <AlertBanner
        open
        severity="warning"
        message="Atencao."
        onClose={() => undefined}
      />,
    );
    expect(await axe(container)).toHaveNoViolations();
  });

  it("severity info", async () => {
    const { container } = render(
      <AlertBanner open severity="info" message="Informativo." />,
    );
    expect(await axe(container)).toHaveNoViolations();
  });
});
