/**
 * A11y tests for UI components:
 *  - ConfirmDialog: aria-describedby present; destructive → Cancel autoFocus;
 *    non-destructive → Confirm autoFocus
 *  - ResponsiveTable: keyboard activation (Enter/Space) fires onRowClick on both
 *    desktop (TableRow) and mobile (Box) views.
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { useMediaQuery } from "@mui/material";
import ConfirmDialog from "@/components/ui/ConfirmDialog";
import { ResponsiveTable, type Column } from "@/components/ui/ResponsiveTable";

vi.mock("@mui/material", async () => {
  const actual = await vi.importActual("@mui/material");
  return { ...(actual as object), useMediaQuery: vi.fn(() => false) };
});

describe("ConfirmDialog — a11y", () => {
  const base = {
    open: true,
    title: "Confirmar ação",
    description: "Você tem certeza?",
    onConfirm: vi.fn(),
    onClose: vi.fn(),
  };

  it("dialog tem aria-describedby apontando para o parágrafo de descrição", () => {
    render(<ConfirmDialog {...base} />);
    const dialog = screen.getByRole("dialog");
    const describedById = dialog.getAttribute("aria-describedby");
    expect(describedById).toBeTruthy();

    // eslint-disable-next-line testing-library/no-node-access
    const descEl = document.getElementById(describedById!);
    expect(descEl).toBeTruthy();
    expect(descEl!).toHaveTextContent("Você tem certeza?");
  });

  it("não-destrutivo → Confirmar recebe autoFocus (está focado ao abrir)", () => {
    render(<ConfirmDialog {...base} destructive={false} />);
    const confirmBtn = screen.getByRole("button", { name: /confirmar/i });
    expect(confirmBtn).toHaveFocus();
  });

  it("destrutivo → Cancelar recebe autoFocus (protege contra ação acidental)", () => {
    render(<ConfirmDialog {...base} destructive />);
    const cancelBtn = screen.getByRole("button", { name: /cancelar/i });
    expect(cancelBtn).toHaveFocus();
  });
});

type Row = { id: string; nome: string };

const COLS: Column[] = [
  { label: "Nome" },
  { label: "Ações", align: "right" },
];

const ROWS: Row[] = [
  { id: "r1", nome: "João" },
  { id: "r2", nome: "Maria" },
];

function renderCell(row: Row, colIndex: number) {
  if (colIndex === 0) return <span>{row.nome}</span>;
  return <button data-testid={`action-${row.id}`}>Edit</button>;
}

describe("ResponsiveTable — keyboard activation (desktop)", () => {
  const onRowClick = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(useMediaQuery).mockReturnValue(false);
  });

  it("TableRow tem tabIndex=0 e role=button quando onRowClick fornecido", () => {
    render(
      <ResponsiveTable
        columns={COLS} rows={ROWS} rowKey={(r) => r.id}
        renderCell={renderCell} onRowClick={onRowClick}
      />,
    );
    const rows = screen.getAllByRole("button", { name: undefined });
    // eslint-disable-next-line testing-library/no-node-access
    const tableRows = document.querySelectorAll("tr[role='button']");
    expect(tableRows.length).toBe(2);
    tableRows.forEach((tr) => {
      expect(tr).toHaveAttribute("tabindex", "0");
    });
  });

  it("Enter dispara onRowClick na linha correta", () => {
    render(
      <ResponsiveTable
        columns={COLS} rows={ROWS} rowKey={(r) => r.id}
        renderCell={renderCell} onRowClick={onRowClick}
      />,
    );
    // eslint-disable-next-line testing-library/no-node-access
    const [firstRow] = document.querySelectorAll("tr[role='button']");
    fireEvent.keyDown(firstRow, { key: "Enter" });
    expect(onRowClick).toHaveBeenCalledWith(ROWS[0]);
  });

  it("Space dispara onRowClick na linha correta", () => {
    render(
      <ResponsiveTable
        columns={COLS} rows={ROWS} rowKey={(r) => r.id}
        renderCell={renderCell} onRowClick={onRowClick}
      />,
    );
    // eslint-disable-next-line testing-library/no-node-access
    const [firstRow] = document.querySelectorAll("tr[role='button']");
    fireEvent.keyDown(firstRow, { key: " " });
    expect(onRowClick).toHaveBeenCalledWith(ROWS[0]);
  });

  it("Tab/outros keys não disparam onRowClick", () => {
    render(
      <ResponsiveTable
        columns={COLS} rows={ROWS} rowKey={(r) => r.id}
        renderCell={renderCell} onRowClick={onRowClick}
      />,
    );
    // eslint-disable-next-line testing-library/no-node-access
    const [firstRow] = document.querySelectorAll("tr[role='button']");
    fireEvent.keyDown(firstRow, { key: "Tab" });
    expect(onRowClick).not.toHaveBeenCalled();
  });

  it("sem onRowClick → TableRow não tem role=button nem tabIndex", () => {
    render(
      <ResponsiveTable
        columns={COLS} rows={ROWS} rowKey={(r) => r.id}
        renderCell={renderCell}
      />,
    );
    // eslint-disable-next-line testing-library/no-node-access
    const tableRows = document.querySelectorAll("tr[role='button']");
    expect(tableRows.length).toBe(0);
  });
});

describe("ResponsiveTable — keyboard activation (mobile)", () => {
  const onRowClick = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(useMediaQuery).mockReturnValue(true);
  });

  it("card row tem tabIndex=0 e role=button quando onRowClick fornecido", () => {
    render(
      <ResponsiveTable
        columns={COLS} rows={ROWS} rowKey={(r) => r.id}
        renderCell={renderCell} onRowClick={onRowClick}
      />,
    );
    // eslint-disable-next-line testing-library/no-node-access
    const cardRows = document.querySelectorAll("[role='button'][tabindex='0']");
    expect(cardRows.length).toBe(2);
  });

  it("Enter dispara onRowClick no card correto (mobile)", () => {
    render(
      <ResponsiveTable
        columns={COLS} rows={ROWS} rowKey={(r) => r.id}
        renderCell={renderCell} onRowClick={onRowClick}
      />,
    );
    // eslint-disable-next-line testing-library/no-node-access
    const [firstCard] = document.querySelectorAll("[role='button'][tabindex='0']");
    fireEvent.keyDown(firstCard, { key: "Enter" });
    expect(onRowClick).toHaveBeenCalledWith(ROWS[0]);
  });

  it("Space dispara onRowClick no card correto (mobile)", () => {
    render(
      <ResponsiveTable
        columns={COLS} rows={ROWS} rowKey={(r) => r.id}
        renderCell={renderCell} onRowClick={onRowClick}
      />,
    );
    // eslint-disable-next-line testing-library/no-node-access
    const [firstCard] = document.querySelectorAll("[role='button'][tabindex='0']");
    fireEvent.keyDown(firstCard, { key: " " });
    expect(onRowClick).toHaveBeenCalledWith(ROWS[0]);
  });
});
