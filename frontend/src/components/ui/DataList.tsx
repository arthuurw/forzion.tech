"use client";
import { Card } from "@mui/material";
import type { ReactNode } from "react";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import EmptyState from "@/components/ui/EmptyState";
import { ResponsiveTable, type Column, type PaginationConfig } from "@/components/ui/ResponsiveTable";

interface Props<T> {
  loading: boolean;
  items: T[];
  emptyMessage: string;
  emptyActionLabel?: string;
  onEmptyAction?: () => void;
  columns: Column[];
  rowKey: (item: T) => string;
  renderCell: (item: T, colIndex: number, rowIndex: number) => ReactNode;
  onRowClick?: (item: T) => void;
  pagination?: PaginationConfig;
}

export default function DataList<T>({
  loading,
  items,
  emptyMessage,
  emptyActionLabel,
  onEmptyAction,
  columns,
  rowKey,
  renderCell,
  onRowClick,
  pagination,
}: Props<T>) {
  return (
    <Card variant="outlined">
      {loading ? (
        <LoadingSpinner />
      ) : items.length === 0 ? (
        <EmptyState message={emptyMessage} actionLabel={emptyActionLabel} onAction={onEmptyAction} />
      ) : (
        <ResponsiveTable
          columns={columns}
          rows={items}
          rowKey={rowKey}
          renderCell={renderCell}
          onRowClick={onRowClick}
          pagination={pagination}
        />
      )}
    </Card>
  );
}
