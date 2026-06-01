// F24 (Fase 5 test remediation) — stateful component story.
// Estados cobertos: populated, empty, com paginacao, mobile-like (via wrapper).
import type { Meta, StoryObj } from "@storybook/nextjs";
import { useState } from "react";
import { Box, Button, Chip, Typography } from "@mui/material";
import { ResponsiveTable, type Column } from "./ResponsiveTable";

type Aluno = {
  id: string;
  nome: string;
  email: string;
  status: "Ativo" | "Inativo" | "AguardandoAprovacao";
};

const COLUMNS: Column[] = [
  { label: "Nome" },
  { label: "E-mail" },
  { label: "Status" },
  { label: "Ações", align: "right" },
];

const ALUNOS: Aluno[] = [
  { id: "a-1", nome: "Maria Silva", email: "maria@forzion.tech", status: "Ativo" },
  { id: "a-2", nome: "João Souza", email: "joao@forzion.tech", status: "AguardandoAprovacao" },
  { id: "a-3", nome: "Ana Costa", email: "ana@forzion.tech", status: "Inativo" },
];

function renderCell(row: Aluno, colIndex: number): React.ReactNode {
  switch (colIndex) {
    case 0:
      return <Typography variant="body2" sx={{ fontWeight: 600 }}>{row.nome}</Typography>;
    case 1:
      return <Typography variant="body2" color="text.secondary">{row.email}</Typography>;
    case 2: {
      const color = row.status === "Ativo" ? "success" : row.status === "Inativo" ? "default" : "warning";
      return <Chip label={row.status} size="small" color={color} />;
    }
    case 3:
      return <Button size="small">Editar</Button>;
    default:
      return null;
  }
}

const meta: Meta<typeof ResponsiveTable<Aluno>> = {
  title: "UI/ResponsiveTable",
  component: ResponsiveTable<Aluno>,
  tags: ["autodocs"],
  parameters: {
    layout: "padded",
  },
};

export default meta;

type Story = StoryObj<typeof ResponsiveTable<Aluno>>;

export const Populated: Story = {
  args: {
    columns: COLUMNS,
    rows: ALUNOS,
    rowKey: (r) => r.id,
    renderCell,
  },
};

export const Empty: Story = {
  args: {
    columns: COLUMNS,
    rows: [],
    rowKey: (r) => r.id,
    renderCell,
  },
};

export const ComOnRowClick: Story = {
  args: {
    columns: COLUMNS,
    rows: ALUNOS,
    rowKey: (r) => r.id,
    renderCell,
    onRowClick: (row) => alert(`Clique em ${row.nome}`),
  },
};

export const ComPaginacao: Story = {
  render: (args) => {
    function Wrapper() {
      const [page, setPage] = useState(0);
      const [rowsPerPage, setRowsPerPage] = useState(5);

      const longList: Aluno[] = Array.from({ length: 23 }, (_, i) => ({
        id: `aluno-${i + 1}`,
        nome: `Aluno ${i + 1}`,
        email: `aluno-${i + 1}@forzion.tech`,
        status: i % 3 === 0 ? "AguardandoAprovacao" : i % 5 === 0 ? "Inativo" : "Ativo",
      }));

      const start = page * rowsPerPage;
      const visible = longList.slice(start, start + rowsPerPage);

      return (
        <Box>
          <ResponsiveTable
            {...args}
            rows={visible}
            pagination={{
              count: longList.length,
              page,
              rowsPerPage,
              onPageChange: setPage,
              onRowsPerPageChange: setRowsPerPage,
              rowsPerPageOptions: [5, 10, 25],
            }}
          />
        </Box>
      );
    }
    return <Wrapper />;
  },
  args: {
    columns: COLUMNS,
    rows: [],
    rowKey: (r: Aluno) => r.id,
    renderCell,
  },
};
