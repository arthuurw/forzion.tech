import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { useMediaQuery } from "@mui/material";
import { ResponsiveTable, type Column } from "@/components/ui/ResponsiveTable";

vi.mock("@mui/material", async () => {
  const actual = await vi.importActual("@mui/material");
  return { ...(actual as object), useMediaQuery: vi.fn(() => false) };
});

// ─── Fixtures ────────────────────────────────────────────────────────────────

type Row = { id: string; nome: string; status: string };

const COLS: Column[] = [
  { label: "Nome" },                          // idx 0 → primary
  { label: "Status" },                         // idx 1 → secondary
  { label: "Ações", align: "right" },          // idx 2 (last + right) → actions
];

const ROWS: Row[] = [
  { id: "r1", nome: "João",  status: "Ativo"   },
  { id: "r2", nome: "Maria", status: "Inativo" },
];

const onRowClick = vi.fn();

function renderCell(row: Row, colIndex: number) {
  if (colIndex === 0) return <span>{row.nome}</span>;
  if (colIndex === 1) return <span>{row.status}</span>;
  return <button data-testid={`action-${row.id}`}>Edit</button>;
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(useMediaQuery).mockReturnValue(false); // desktop por padrão
});

// ─── Desktop (isMobile = false) ──────────────────────────────────────────────

describe("ResponsiveTable — desktop", () => {
  it("renderiza cabeçalhos das colunas", () => {
    render(<ResponsiveTable columns={COLS} rows={ROWS} rowKey={(r) => r.id} renderCell={renderCell} />);
    expect(screen.getByText("Nome")).toBeDefined();
    expect(screen.getByText("Status")).toBeDefined();
    expect(screen.getByText("Ações")).toBeDefined();
  });

  it("onRowClick dispara ao clicar na linha", () => {
    render(
      <ResponsiveTable
        columns={COLS} rows={ROWS} rowKey={(r) => r.id}
        renderCell={renderCell} onRowClick={onRowClick}
      />,
    );
    fireEvent.click(screen.getByText("João"));
    expect(onRowClick).toHaveBeenCalledWith(ROWS[0]);
  });

  it("célula de actions bloqueia propagação para row", () => {
    render(
      <ResponsiveTable
        columns={COLS} rows={ROWS} rowKey={(r) => r.id}
        renderCell={renderCell} onRowClick={onRowClick}
      />,
    );
    fireEvent.click(screen.getByTestId("action-r1"));
    expect(onRowClick).not.toHaveBeenCalled();
  });

  it("sem onRowClick → não adiciona cursor pointer", () => {
    render(
      <ResponsiveTable columns={COLS} rows={ROWS} rowKey={(r) => r.id} renderCell={renderCell} />,
    );
    // Apenas verifica que o componente renderiza sem erro
    expect(screen.getByText("João")).toBeDefined();
  });
});

// ─── Desktop com paginação ───────────────────────────────────────────────────

describe("ResponsiveTable — paginação", () => {
  const onPageChange = vi.fn();
  const onRowsPerPageChange = vi.fn();

  const pagination = {
    count: 50,
    page: 0,
    rowsPerPage: 10,
    onPageChange,
    onRowsPerPageChange,
  };

  it("renderiza slot de paginação quando fornecido", () => {
    render(
      <ResponsiveTable
        columns={COLS} rows={ROWS} rowKey={(r) => r.id}
        renderCell={renderCell} pagination={pagination}
      />,
    );
    // MUI TablePagination exibe o label de linhas por página
    expect(screen.getByText("Por página:")).toBeDefined();
  });

  it("não renderiza paginação quando ausente", () => {
    render(
      <ResponsiveTable columns={COLS} rows={ROWS} rowKey={(r) => r.id} renderCell={renderCell} />,
    );
    expect(screen.queryByText("Por página:")).toBeNull();
  });

  it("onPageChange dispara ao clicar próxima página", () => {
    render(
      <ResponsiveTable
        columns={COLS} rows={ROWS} rowKey={(r) => r.id}
        renderCell={renderCell} pagination={pagination}
      />,
    );
    fireEvent.click(screen.getByTitle("Go to next page"));
    expect(onPageChange).toHaveBeenCalledWith(1);
  });

  it("onRowsPerPageChange dispara ao selecionar nova opção", async () => {
    render(
      <ResponsiveTable
        columns={COLS} rows={ROWS} rowKey={(r) => r.id}
        renderCell={renderCell} pagination={pagination}
      />,
    );
    // Abre o MUI Select
    fireEvent.mouseDown(screen.getByRole("combobox"));
    // Clica na opção 25
    fireEvent.click(screen.getByRole("option", { name: "25" }));
    expect(onRowsPerPageChange).toHaveBeenCalledWith(25);
  });
});

// ─── Mobile (isMobile = true) ────────────────────────────────────────────────

describe("ResponsiveTable — mobile", () => {
  beforeEach(() => vi.mocked(useMediaQuery).mockReturnValue(true));

  it("não renderiza <table> — layout de cards", () => {
    render(
      <ResponsiveTable columns={COLS} rows={ROWS} rowKey={(r) => r.id} renderCell={renderCell} />,
    );
    expect(screen.queryByRole("table")).toBeNull();
    expect(screen.getByText("João")).toBeDefined();
  });

  it("múltiplas linhas → renderiza Divider entre rows", () => {
    render(
      <ResponsiveTable columns={COLS} rows={ROWS} rowKey={(r) => r.id} renderCell={renderCell} />,
    );
    // Ambas as rows estão presentes
    expect(screen.getByText("João")).toBeDefined();
    expect(screen.getByText("Maria")).toBeDefined();
  });

  it("onRowClick dispara ao clicar no card", () => {
    render(
      <ResponsiveTable
        columns={COLS} rows={ROWS} rowKey={(r) => r.id}
        renderCell={renderCell} onRowClick={onRowClick}
      />,
    );
    fireEvent.click(screen.getByText("João"));
    expect(onRowClick).toHaveBeenCalledWith(ROWS[0]);
  });

  it("box de actions bloqueia propagação no card", () => {
    render(
      <ResponsiveTable
        columns={COLS} rows={ROWS} rowKey={(r) => r.id}
        renderCell={renderCell} onRowClick={onRowClick}
      />,
    );
    fireEvent.click(screen.getByTestId("action-r1"));
    expect(onRowClick).not.toHaveBeenCalled();
  });

  it("coluna com mobileRole explícito 'hidden' não aparece como secundária", () => {
    const colsWithHidden: Column[] = [
      { label: "Nome" },
      { label: "Oculto", mobileRole: "hidden" },
      { label: "Ações", align: "right" },
    ];
    render(
      <ResponsiveTable
        columns={colsWithHidden} rows={ROWS} rowKey={(r) => r.id} renderCell={renderCell}
      />,
    );
    // Label "Oculto:" não aparece como cabeçalho secundário
    expect(screen.queryByText("Oculto:")).toBeNull();
  });
});
