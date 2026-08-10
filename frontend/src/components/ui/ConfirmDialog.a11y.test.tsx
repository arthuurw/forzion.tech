import { render } from "@testing-library/react";
import { axe } from "vitest-axe";
import { describe, it, expect, vi } from "vitest";
import ConfirmDialog from "./ConfirmDialog";

describe("ConfirmDialog a11y", () => {
  const base = {
    open: true,
    title: "Confirmar ação",
    description: "Você tem certeza?",
    onConfirm: vi.fn(),
    onClose: vi.fn(),
  };

  it("variante não-destrutiva aberta sem violações", async () => {
    const { container } = render(<ConfirmDialog {...base} />);
    expect(await axe(container)).toHaveNoViolations();
  });

  it("variante destrutiva aberta sem violações", async () => {
    const { container } = render(<ConfirmDialog {...base} destructive />);
    expect(await axe(container)).toHaveNoViolations();
  });
});
