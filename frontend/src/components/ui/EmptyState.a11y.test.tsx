import { render } from "@testing-library/react";
import { axe } from "vitest-axe";
import { describe, it, expect } from "vitest";
import EmptyState from "./EmptyState";

describe("EmptyState a11y", () => {
  it("apenas mensagem", async () => {
    const { container } = render(<EmptyState message="Nada por aqui." />);
    expect(await axe(container)).toHaveNoViolations();
  });

  it("com action button", async () => {
    const { container } = render(
      <EmptyState
        message="Adicione o primeiro item."
        actionLabel="Criar"
        onAction={() => undefined}
      />,
    );
    expect(await axe(container)).toHaveNoViolations();
  });
});
