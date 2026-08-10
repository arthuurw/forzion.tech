import { render } from "@testing-library/react";
import { axe } from "vitest-axe";
import { describe, it, expect } from "vitest";
import DetalheErro from "./DetalheErro";

describe("DetalheErro a11y", () => {
  it("sem ações sem violações", async () => {
    const { container } = render(<DetalheErro mensagem="Não foi possível carregar os detalhes." />);
    expect(await axe(container)).toHaveNoViolations();
  });

  it("com onRetry e onVoltar sem violações", async () => {
    const { container } = render(
      <DetalheErro
        mensagem="Não foi possível carregar os detalhes."
        onRetry={() => undefined}
        onVoltar={() => undefined}
      />,
    );
    expect(await axe(container)).toHaveNoViolations();
  });
});
