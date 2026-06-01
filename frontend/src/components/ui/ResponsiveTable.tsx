"use client";
import {
  Box, Divider, Table, TableHead, TableRow, TableCell, TableBody,
  TablePagination, Typography, Stack, useMediaQuery, useTheme,
} from "@mui/material";
import type { ReactNode } from "react";

export type Column = {
  label: string;
  align?: "left" | "right" | "center";
  mobileRole?: "primary" | "secondary" | "actions" | "hidden";
};

export type PaginationConfig = {
  count: number;
  page: number;
  rowsPerPage: number;
  onPageChange: (page: number) => void;
  onRowsPerPageChange: (rowsPerPage: number) => void;
  rowsPerPageOptions?: number[];
};

type Props<T> = {
  columns: Column[];
  rows: T[];
  rowKey: (row: T) => string;
  renderCell: (row: T, colIndex: number, rowIndex: number) => ReactNode;
  onRowClick?: (row: T) => void;
  pagination?: PaginationConfig;
};

function resolveRole(
  col: Column,
  index: number,
  isLast: boolean,
): "primary" | "secondary" | "actions" | "hidden" {
  if (col.mobileRole) return col.mobileRole;
  if (index === 0) return "primary";
  if (isLast && col.align === "right") return "actions";
  return "secondary";
}

export function ResponsiveTable<T>({
  columns,
  rows,
  rowKey,
  renderCell,
  onRowClick,
  pagination,
}: Props<T>) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("md"));

  const roles = columns.map((col, i) => resolveRole(col, i, i === columns.length - 1));
  const primaryIdx = roles.indexOf("primary");
  const actionsIdx = roles.indexOf("actions");
  const secondaryIdxs = roles.flatMap((role, i) => (role === "secondary" ? [i] : []));

  const paginationSlot = pagination ? (
    <TablePagination
      component="div"
      count={pagination.count}
      page={pagination.page}
      rowsPerPage={pagination.rowsPerPage}
      onPageChange={(_, p) => pagination.onPageChange(p)}
      onRowsPerPageChange={(e) => pagination.onRowsPerPageChange(+e.target.value)}
      rowsPerPageOptions={pagination.rowsPerPageOptions ?? [5, 10, 25]}
      labelRowsPerPage="Por página:"
      labelDisplayedRows={({ from, to, count }) => `${from}–${to} de ${count}`}
    />
  ) : null;

  if (isMobile) {
    return (
      <>
        {rows.map((row, rowIndex) => (
          <Box key={rowKey(row)}>
            {rowIndex > 0 && <Divider />}
            <Box
              onClick={onRowClick ? () => onRowClick(row) : undefined}
              tabIndex={onRowClick ? 0 : undefined}
              role={onRowClick ? "button" : undefined}
              onKeyDown={
                onRowClick
                  ? (e) => {
                      if (e.key === "Enter" || e.key === " ") {
                        e.preventDefault();
                        onRowClick(row);
                      }
                    }
                  : undefined
              }
              sx={{
                px: 2,
                py: 1.5,
                cursor: onRowClick ? "pointer" : "default",
                "&:hover": onRowClick ? { bgcolor: "action.hover" } : {},
              }}
            >
              <Box sx={{ display: "flex", alignItems: "flex-start", justifyContent: "space-between", gap: 1 }}>
                <Box sx={{ flex: 1, minWidth: 0 }}>
                  {primaryIdx >= 0 && renderCell(row, primaryIdx, rowIndex)}
                </Box>
                {actionsIdx >= 0 && (
                  <Box onClick={(e) => e.stopPropagation()} sx={{ flexShrink: 0 }}>
                    {renderCell(row, actionsIdx, rowIndex)}
                  </Box>
                )}
              </Box>
              {secondaryIdxs.length > 0 && (
                <Stack spacing={0.5} sx={{ mt: 0.75 }}>
                  {secondaryIdxs.map((colIdx) => (
                    <Box
                      key={colIdx}
                      sx={{ display: "flex", gap: 1, alignItems: "center", flexWrap: "wrap" }}
                    >
                      <Typography
                        variant="caption"
                        color="text.secondary"
                        sx={{ flexShrink: 0 }}
                      >
                        {columns[colIdx].label}:
                      </Typography>
                      <Box>{renderCell(row, colIdx, rowIndex)}</Box>
                    </Box>
                  ))}
                </Stack>
              )}
            </Box>
          </Box>
        ))}
        {paginationSlot}
      </>
    );
  }

  return (
    <>
      <Box sx={{ overflowX: "auto" }}>
        <Table size="small">
          <TableHead>
            <TableRow>
              {columns.map((col, i) => (
                <TableCell key={i} align={col.align} sx={{ fontWeight: 600 }}>
                  {col.label}
                </TableCell>
              ))}
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.map((row, rowIndex) => (
              <TableRow
                key={rowKey(row)}
                hover
                onClick={onRowClick ? () => onRowClick(row) : undefined}
                tabIndex={onRowClick ? 0 : undefined}
                role={onRowClick ? "button" : undefined}
                onKeyDown={
                  onRowClick
                    ? (e) => {
                        if (e.key === "Enter" || e.key === " ") {
                          e.preventDefault();
                          onRowClick(row);
                        }
                      }
                    : undefined
                }
                sx={{ cursor: onRowClick ? "pointer" : "default" }}
              >
                {columns.map((col, i) => (
                  <TableCell
                    key={i}
                    align={col.align}
                    onClick={
                      roles[i] === "actions" && onRowClick
                        ? (e) => e.stopPropagation()
                        : undefined
                    }
                  >
                    {renderCell(row, i, rowIndex)}
                  </TableCell>
                ))}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Box>
      {paginationSlot}
    </>
  );
}
