import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import CopiarPixButton from "../CopiarPixButton";

describe("CopiarPixButton", () => {
  beforeEach(() => {
    Object.assign(navigator, { clipboard: { writeText: vi.fn() } });
  });

  it("sucesso → mostra confirmação de cópia", async () => {
    (navigator.clipboard.writeText as ReturnType<typeof vi.fn>).mockResolvedValue(undefined);
    render(<CopiarPixButton codigo="pix-123" />);

    fireEvent.click(screen.getByRole("button", { name: "Copiar código" }));

    expect(await screen.findByText("Código copiado!")).toBeInTheDocument();
    expect(navigator.clipboard.writeText).toHaveBeenCalledWith("pix-123");
  });

  it("falha → mostra instrução de cópia manual", async () => {
    (navigator.clipboard.writeText as ReturnType<typeof vi.fn>).mockRejectedValue(new Error("denied"));
    render(<CopiarPixButton codigo="pix-123" />);

    fireEvent.click(screen.getByRole("button", { name: "Copiar código" }));

    expect(
      await screen.findByText("Não foi possível copiar. Selecione e copie o código manualmente."),
    ).toBeInTheDocument();
  });
});
