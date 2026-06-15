import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import StatusChip from "@/components/ui/StatusChip";
import EmptyState from "@/components/ui/EmptyState";
import ConfirmDialog from "@/components/ui/ConfirmDialog";
import InfoLine from "@/components/ui/InfoLine";

describe("StatusChip", () => {
  it("renderiza 'Ativo' para status Ativo", () => {
    render(<StatusChip status="Ativo" />);
    expect(screen.getByText("Ativo")).toBeInTheDocument();
  });

  it("renderiza 'Inativo' para status Inativo", () => {
    render(<StatusChip status="Inativo" />);
    expect(screen.getByText("Inativo")).toBeInTheDocument();
  });

  it("renderiza 'Aguardando' para status AguardandoAprovacao", () => {
    render(<StatusChip status="AguardandoAprovacao" />);
    expect(screen.getByText("Aguardando")).toBeInTheDocument();
  });
});

describe("EmptyState", () => {
  it("renderiza mensagem passada via props", () => {
    render(<EmptyState message="Nenhum item encontrado" />);
    expect(screen.getByText("Nenhum item encontrado")).toBeInTheDocument();
  });

  it("renderiza botão de ação quando actionLabel e onAction fornecidos", () => {
    const onAction = vi.fn();
    render(<EmptyState message="Vazio" actionLabel="Adicionar" onAction={onAction} />);
    const btn = screen.getByText("Adicionar");
    expect(btn).toBeInTheDocument();
    fireEvent.click(btn);
    expect(onAction).toHaveBeenCalledOnce();
  });

  it("não renderiza botão quando actionLabel ausente", () => {
    render(<EmptyState message="Vazio" />);
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
  });
});

describe("InfoLine", () => {
  it("renderiza label e value", () => {
    render(<InfoLine label="E-mail" value="ana@x.com" />);
    expect(screen.getByText("E-mail:")).toBeInTheDocument();
    expect(screen.getByText(/ana@x\.com/)).toBeInTheDocument();
  });

  it("aplica overflowWrap:anywhere p/ quebrar e-mail longo sem espaço (R9)", () => {
    render(
      <InfoLine label="E-mail" value="nome.sobrenome.muito.longo@dominio-extenso.com.br" />,
    );
    expect(screen.getByText("E-mail:").closest("p")).toHaveStyle({
      "overflow-wrap": "anywhere",
    });
  });
});

describe("ConfirmDialog", () => {
  const baseProps = {
    open: true,
    title: "Confirmar exclusão",
    description: "Tem certeza?",
    onConfirm: vi.fn(),
    onClose: vi.fn(),
  };

  it("aberto → renderiza título e descrição", () => {
    render(<ConfirmDialog {...baseProps} />);
    expect(screen.getByText("Confirmar exclusão")).toBeInTheDocument();
    expect(screen.getByText("Tem certeza?")).toBeInTheDocument();
  });

  it("botão cancelar chama onClose", () => {
    const onClose = vi.fn();
    render(<ConfirmDialog {...baseProps} onClose={onClose} />);
    fireEvent.click(screen.getByText("Cancelar"));
    expect(onClose).toHaveBeenCalledOnce();
  });

  it("botão confirmar chama onConfirm", () => {
    const onConfirm = vi.fn();
    render(<ConfirmDialog {...baseProps} onConfirm={onConfirm} />);
    fireEvent.click(screen.getByText("Confirmar"));
    expect(onConfirm).toHaveBeenCalledOnce();
  });

  it("fechado → não renderiza conteúdo", () => {
    render(<ConfirmDialog {...baseProps} open={false} />);
    expect(screen.queryByText("Confirmar exclusão")).not.toBeInTheDocument();
  });

  it("loading → botões desabilitados", () => {
    render(<ConfirmDialog {...baseProps} loading />);
    const buttons = screen.getAllByRole("button");
    buttons.forEach((btn) => expect(btn).toBeDisabled());
  });
});
