import { useState } from "react";

interface CRUDDialogState<T> {
  createOpen: boolean;
  openCreate: () => void;
  closeCreate: () => void;
  creating: boolean;
  setCreating: (v: boolean) => void;

  editTarget: T | null;
  openEdit: (item: T) => void;
  closeEdit: () => void;
  editing: boolean;
  setEditing: (v: boolean) => void;

  deleteTarget: T | null;
  openDelete: (item: T) => void;
  closeDelete: () => void;
  deleting: boolean;
  setDeleting: (v: boolean) => void;
}

export function useCRUDDialog<T>(): CRUDDialogState<T> {
  const [createOpen, setCreateOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<T | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<T | null>(null);
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState(false);
  const [deleting, setDeleting] = useState(false);

  return {
    createOpen,
    openCreate: () => setCreateOpen(true),
    closeCreate: () => setCreateOpen(false),
    creating,
    setCreating,

    editTarget,
    openEdit: (item) => setEditTarget(item),
    closeEdit: () => setEditTarget(null),
    editing,
    setEditing,

    deleteTarget,
    openDelete: (item) => setDeleteTarget(item),
    closeDelete: () => setDeleteTarget(null),
    deleting,
    setDeleting,
  };
}
