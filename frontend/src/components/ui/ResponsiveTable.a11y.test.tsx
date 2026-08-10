import { render } from "@testing-library/react";
import { axe } from "vitest-axe";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { useMediaQuery } from "@mui/material";
import { ResponsiveTable, type Column } from "./ResponsiveTable";

vi.mock("@mui/material", async () => {
  const actual = await vi.importActual("@mui/material");
  return { ...(actual as object), useMediaQuery: vi.fn(() => false) };
});

type Row = { id: string; nome: string };

const COLS: Column[] = [{ label: "Nome" }, { label: "Ações", align: "right" }];

const ROWS: Row[] = [
  { id: "r1", nome: "João" },
  { id: "r2", nome: "Maria" },
];

function renderCell(row: Row, colIndex: number) {
  if (colIndex === 0) return <span>{row.nome}</span>;
  return <button>Editar {row.nome}</button>;
}

describe("ResponsiveTable a11y", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("modo desktop (table) sem violações", async () => {
    vi.mocked(useMediaQuery).mockReturnValue(false);
    const { container } = render(
      <ResponsiveTable columns={COLS} rows={ROWS} rowKey={(r) => r.id} renderCell={renderCell} />,
    );
    expect(await axe(container)).toHaveNoViolations();
  });

  it("modo mobile (cards) sem violações", async () => {
    vi.mocked(useMediaQuery).mockReturnValue(true);
    const { container } = render(
      <ResponsiveTable columns={COLS} rows={ROWS} rowKey={(r) => r.id} renderCell={renderCell} />,
    );
    expect(await axe(container)).toHaveNoViolations();
  });
});
