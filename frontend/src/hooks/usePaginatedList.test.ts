import { describe, it, expect, vi } from "vitest";
import { renderHook, act, waitFor } from "@testing-library/react";
import { usePaginatedList } from "@/hooks/usePaginatedList";
import type { PaginatedResponse } from "@/types";

function page(items: number[], total = items.length): PaginatedResponse<number> {
  return { items, total, pagina: 0, tamanhoPagina: 10 } as unknown as PaginatedResponse<number>;
}

describe("usePaginatedList", () => {
  it("carrega items na montagem", async () => {
    const fetcher = vi.fn().mockResolvedValue(page([1, 2, 3], 3));
    const { result } = renderHook(() => usePaginatedList({ fetcher }));

    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.items).toEqual([1, 2, 3]);
    expect(result.current.total).toBe(3);
    expect(result.current.error).toBe("");
  });

  it("erro no fetch popula error com errorMessage", async () => {
    const fetcher = vi.fn().mockRejectedValue(new Error("boom"));
    const { result } = renderHook(() =>
      usePaginatedList({ fetcher, errorMessage: "Falhou" }),
    );

    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.error).toBe("Falhou");
    expect(result.current.items).toEqual([]);
  });

  it("setPage dispara novo fetch com a página", async () => {
    const fetcher = vi.fn().mockResolvedValue(page([1]));
    const { result } = renderHook(() => usePaginatedList({ fetcher }));
    await waitFor(() => expect(result.current.loading).toBe(false));

    act(() => result.current.setPage(2));
    await waitFor(() => expect(fetcher).toHaveBeenCalledWith(2, expect.any(Number), expect.any(AbortSignal)));
    expect(result.current.page).toBe(2);
  });

  it("setPageSize reseta página para 0", async () => {
    const fetcher = vi.fn().mockResolvedValue(page([1]));
    const { result } = renderHook(() => usePaginatedList({ fetcher, initialPageSize: 10 }));
    await waitFor(() => expect(result.current.loading).toBe(false));

    act(() => result.current.setPage(3));
    await waitFor(() => expect(result.current.page).toBe(3));

    act(() => result.current.setPageSize(25));
    await waitFor(() => expect(result.current.pageSize).toBe(25));
    expect(result.current.page).toBe(0);
  });

  it("reload re-executa o fetcher", async () => {
    const fetcher = vi.fn().mockResolvedValue(page([1]));
    const { result } = renderHook(() => usePaginatedList({ fetcher }));
    await waitFor(() => expect(result.current.loading).toBe(false));
    const callsBefore = fetcher.mock.calls.length;

    act(() => result.current.reload());
    await waitFor(() => expect(fetcher.mock.calls.length).toBeGreaterThan(callsBefore));
  });

  it("setSuccess atualiza success", async () => {
    const fetcher = vi.fn().mockResolvedValue(page([1]));
    const { result } = renderHook(() => usePaginatedList({ fetcher }));
    await waitFor(() => expect(result.current.loading).toBe(false));

    act(() => result.current.setSuccess("ok"));
    expect(result.current.success).toBe("ok");
  });

  it("fetch de request abortada não atualiza estado (signal.aborted)", async () => {
    // Deferred: controla quando o primeiro fetch resolve. reload() aborta o
    // controller anterior; ao resolver depois, signal.aborted=true e os
    // setItems/setTotal/setLoading guardados por !aborted são pulados.
    let resolveFirst!: (v: PaginatedResponse<number>) => void;
    const first = new Promise<PaginatedResponse<number>>((r) => {
      resolveFirst = r;
    });
    const fetcher = vi
      .fn<(p: number, ps: number, s: AbortSignal) => Promise<PaginatedResponse<number>>>()
      .mockReturnValueOnce(first)
      .mockResolvedValue(page([9], 9));

    const { result } = renderHook(() => usePaginatedList({ fetcher }));

    // Segundo carregamento aborta o primeiro (in-flight).
    act(() => result.current.reload());
    await waitFor(() => expect(result.current.items).toEqual([9]));

    // Resolve o primeiro APÓS abortado: não deve sobrescrever items=[9].
    act(() => resolveFirst(page([1, 2, 3], 3)));
    await Promise.resolve();
    expect(result.current.items).toEqual([9]);
    expect(result.current.total).toBe(9);
  });

  it("erro de request abortada não popula error", async () => {
    let rejectFirst!: (e: unknown) => void;
    const first = new Promise<PaginatedResponse<number>>((_, rej) => {
      rejectFirst = rej;
    });
    const fetcher = vi
      .fn<(p: number, ps: number, s: AbortSignal) => Promise<PaginatedResponse<number>>>()
      .mockReturnValueOnce(first)
      .mockResolvedValue(page([5], 5));

    const { result } = renderHook(() => usePaginatedList({ fetcher, errorMessage: "Falhou" }));
    act(() => result.current.reload());
    await waitFor(() => expect(result.current.items).toEqual([5]));

    act(() => rejectFirst(new Error("aborted")));
    await Promise.resolve();
    expect(result.current.error).toBe("");
  });
});
