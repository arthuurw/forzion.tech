"use client";
import { useState, useCallback } from "react";
import { useRouter } from "next/navigation";
import {
  Box, Typography, FormControl, InputLabel, Select, MenuItem,
  TextField, IconButton, Tooltip,
} from "@mui/material";
import InfoIcon from "@mui/icons-material/Info";
import StatusChip from "@/components/ui/StatusChip";
import AlertBanner from "@/components/ui/AlertBanner";
import PageHeader from "@/components/ui/PageHeader";
import DataList from "@/components/ui/DataList";
import type { Column } from "@/components/ui/ResponsiveTable";
import { adminApi } from "@/lib/api/admin";
import type { AlunoResponse, AlunoStatus } from "@/types";
import { usePaginatedList } from "@/hooks/usePaginatedList";

const COLUMNS: Column[] = [
  { label: "Nome" },
  { label: "E-mail" },
  { label: "Status" },
  { label: "Cadastro" },
  { label: "Ações", align: "right" },
];

export default function AlunosAdminPage() {
  const router = useRouter();
  const [statusFilter, setStatusFilter] = useState<AlunoStatus | "">("");
  const [nomeFilter, setNomeFilter] = useState("");
  const [nomeInput, setNomeInput] = useState("");

  const fetcher = useCallback(
    (p: number, ps: number) =>
      adminApi.listAlunos({
        nome: nomeFilter || undefined,
        status: statusFilter || undefined,
        pagina: p + 1,
        tamanhoPagina: ps,
      }).then((r) => r.data),
    [nomeFilter, statusFilter]
  );

  const { items, total, page, pageSize, loading, error, setPage, setPageSize, setError } =
    usePaginatedList<AlunoResponse>({ fetcher, errorMessage: "Erro ao carregar alunos." });

  const applyNome = () => {
    setNomeFilter(nomeInput.trim());
    setPage(0);
  };

  return (
    <Box>
      <PageHeader title="Alunos" />

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />

      <Box sx={{ display: "flex", gap: 2, mb: 2, flexWrap: "wrap" }}>
        <TextField
          label="Buscar por nome"
          value={nomeInput}
          onChange={(e) => setNomeInput(e.target.value)}
          onKeyDown={(e) => { if (e.key === "Enter") applyNome(); }}
          size="small"
          sx={{ minWidth: 220 }}
        />
        <FormControl size="small" sx={{ minWidth: 160 }}>
          <InputLabel>Status</InputLabel>
          <Select
            value={statusFilter}
            label="Status"
            onChange={(e) => { setStatusFilter(e.target.value as AlunoStatus | ""); setPage(0); }}
          >
            <MenuItem value="">Todos</MenuItem>
            <MenuItem value="AguardandoAprovacao">Aguardando</MenuItem>
            <MenuItem value="Ativo">Ativo</MenuItem>
            <MenuItem value="Inativo">Inativo</MenuItem>
          </Select>
        </FormControl>
      </Box>

      <DataList
        loading={loading}
        items={items}
        emptyMessage="Nenhum aluno encontrado para os filtros aplicados."
        columns={COLUMNS}
        rowKey={(a) => a.alunoId}
        pagination={{ count: total, page, rowsPerPage: pageSize, onPageChange: setPage, onRowsPerPageChange: setPageSize }}
        renderCell={(a, i) => {
          if (i === 0) return <Typography variant="body2" sx={{ fontWeight: 500 }}>{a.nome}</Typography>;
          if (i === 1) return <Typography variant="body2" color="text.secondary">{a.email ?? "—"}</Typography>;
          if (i === 2) return <StatusChip status={a.status} />;
          if (i === 3) return (
            <Typography variant="caption">{new Date(a.createdAt).toLocaleDateString("pt-BR")}</Typography>
          );
          return (
            <Tooltip title="Ver detalhe">
              <IconButton size="small" onClick={() => router.push(`/admin/alunos/${a.alunoId}`)}>
                <InfoIcon fontSize="small" />
              </IconButton>
            </Tooltip>
          );
        }}
      />
    </Box>
  );
}
