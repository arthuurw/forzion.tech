import { render } from "@testing-library/react";
import { axe } from "vitest-axe";
import { describe, it, expect } from "vitest";
import StatusChip from "./StatusChip";

describe("StatusChip a11y", () => {
  it("status AguardandoAprovacao (warning)", async () => {
    const { container } = render(<StatusChip status="AguardandoAprovacao" />);
    expect(await axe(container)).toHaveNoViolations();
  });

  it("status Ativo (success)", async () => {
    const { container } = render(<StatusChip status="Ativo" />);
    expect(await axe(container)).toHaveNoViolations();
  });

  it("status Inativo (error)", async () => {
    const { container } = render(<StatusChip status="Inativo" />);
    expect(await axe(container)).toHaveNoViolations();
  });

  it("size medium", async () => {
    const { container } = render(<StatusChip status="Ativo" size="medium" />);
    expect(await axe(container)).toHaveNoViolations();
  });
});
