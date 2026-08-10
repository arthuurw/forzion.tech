import { render } from "@testing-library/react";
import { axe } from "vitest-axe";
import { describe, it, expect } from "vitest";
import Logo from "./Logo";

describe("Logo a11y", () => {
  it("tamanho sm sem violações", async () => {
    const { container } = render(<Logo size="sm" />);
    expect(await axe(container)).toHaveNoViolations();
  });

  it("tamanho md sem violações", async () => {
    const { container } = render(<Logo size="md" />);
    expect(await axe(container)).toHaveNoViolations();
  });

  it("tamanho lg sem violações", async () => {
    const { container } = render(<Logo size="lg" />);
    expect(await axe(container)).toHaveNoViolations();
  });
});
