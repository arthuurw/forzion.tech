import { describe, it, expect } from "vitest";
import { renderHook, act } from "@testing-library/react";
import { useCRUDDialog } from "@/hooks/useCRUDDialog";

interface Item {
  id: string;
}

describe("useCRUDDialog", () => {
  it("estado inicial fechado", () => {
    const { result } = renderHook(() => useCRUDDialog<Item>());
    expect(result.current.createOpen).toBe(false);
    expect(result.current.editTarget).toBeNull();
    expect(result.current.deleteTarget).toBeNull();
    expect(result.current.creating).toBe(false);
    expect(result.current.editing).toBe(false);
    expect(result.current.deleting).toBe(false);
  });

  it("create: open/close + flag creating", () => {
    const { result } = renderHook(() => useCRUDDialog<Item>());
    act(() => result.current.openCreate());
    expect(result.current.createOpen).toBe(true);
    act(() => result.current.setCreating(true));
    expect(result.current.creating).toBe(true);
    act(() => result.current.closeCreate());
    expect(result.current.createOpen).toBe(false);
  });

  it("edit: open com target + close + flag editing", () => {
    const { result } = renderHook(() => useCRUDDialog<Item>());
    act(() => result.current.openEdit({ id: "e1" }));
    expect(result.current.editTarget).toEqual({ id: "e1" });
    act(() => result.current.setEditing(true));
    expect(result.current.editing).toBe(true);
    act(() => result.current.closeEdit());
    expect(result.current.editTarget).toBeNull();
  });

  it("delete: open com target + close + flag deleting", () => {
    const { result } = renderHook(() => useCRUDDialog<Item>());
    act(() => result.current.openDelete({ id: "d1" }));
    expect(result.current.deleteTarget).toEqual({ id: "d1" });
    act(() => result.current.setDeleting(true));
    expect(result.current.deleting).toBe(true);
    act(() => result.current.closeDelete());
    expect(result.current.deleteTarget).toBeNull();
  });
});
