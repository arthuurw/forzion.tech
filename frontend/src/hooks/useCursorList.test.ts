import { describe, it, expect, vi } from "vitest";
import { renderHook, act, waitFor } from "@testing-library/react";
import { useCursorList } from "@/hooks/useCursorList";

type Row = { id: string };

function makeResponse(itens: Row[], proximoCursor?: string | null) {
  return { data: { itens, proximoCursor: proximoCursor ?? null } };
}

describe("useCursorList", () => {
  it("carrega itens na montagem", async () => {
    const rows = [{ id: "a" }];
    const fetcher = vi.fn().mockResolvedValue(makeResponse(rows));
    const { result } = renderHook(() =>
      useCursorList<Row, undefined>({ fetcher, filtro: undefined }),
    );

    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.itens).toEqual(rows);
    expect(result.current.cursor).toBeNull();
    expect(result.current.error).toBe("");
  });

  it("cursor preenchido quando backend retorna proximoCursor", async () => {
    const fetcher = vi.fn().mockResolvedValue(makeResponse([{ id: "x" }], "cursor-1"));
    const { result } = renderHook(() =>
      useCursorList<Row, undefined>({ fetcher, filtro: undefined }),
    );

    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.cursor).toBe("cursor-1");
  });

  it("erro no fetch popula error com errorMessage", async () => {
    const fetcher = vi.fn().mockRejectedValue(new Error("boom"));
    const { result } = renderHook(() =>
      useCursorList<Row, undefined>({
        fetcher,
        filtro: undefined,
        errorMessage: "Falhou ao carregar.",
      }),
    );

    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.error).toBe("Falhou ao carregar.");
    expect(result.current.itens).toEqual([]);
  });

  it("mudança de filtro aborta request anterior e não sobrescreve estado com resultado stale", async () => {
    let resolveFirst!: (v: ReturnType<typeof makeResponse>) => void;
    const firstPromise = new Promise<ReturnType<typeof makeResponse>>((r) => { resolveFirst = r; });

    const fetcher = vi
      .fn<(f: string, aposId: string | undefined, s: AbortSignal) => Promise<ReturnType<typeof makeResponse>>>()
      .mockReturnValueOnce(firstPromise)
      .mockResolvedValue(makeResponse([{ id: "novo" }]));

    const { result, rerender } = renderHook(
      ({ filtro }: { filtro: string }) =>
        useCursorList<Row, string>({ fetcher, filtro, errorMessage: "Erro." }),
      { initialProps: { filtro: "Pendente" } },
    );

    rerender({ filtro: "Emitida" });
    await waitFor(() => expect(result.current.itens).toEqual([{ id: "novo" }]));

    act(() => resolveFirst(makeResponse([{ id: "stale" }])));
    await Promise.resolve();

    expect(result.current.itens).toEqual([{ id: "novo" }]);
  });

  it("request abortada (unmount) não atualiza error", async () => {
    let rejectFirst!: (e: unknown) => void;
    const firstPromise = new Promise<ReturnType<typeof makeResponse>>((_, rej) => { rejectFirst = rej; });

    const fetcher = vi.fn().mockReturnValueOnce(firstPromise);
    const { result, unmount } = renderHook(() =>
      useCursorList<Row, undefined>({ fetcher, filtro: undefined, errorMessage: "Falhou." }),
    );

    unmount();
    act(() => rejectFirst(new Error("aborted")));
    await Promise.resolve();

    expect(result.current.error).toBe("");
  });

  it("carregarMais anexa itens e atualiza cursor", async () => {
    const fetcher = vi
      .fn()
      .mockResolvedValueOnce(makeResponse([{ id: "p1" }], "c1"))
      .mockResolvedValue(makeResponse([{ id: "p2" }], null));

    const { result } = renderHook(() =>
      useCursorList<Row, undefined>({ fetcher, filtro: undefined }),
    );

    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.cursor).toBe("c1");

    await act(() => result.current.carregarMais());

    expect(result.current.itens).toEqual([{ id: "p1" }, { id: "p2" }]);
    expect(result.current.cursor).toBeNull();
  });

  it("reload reinicia a lista e refaz o fetch", async () => {
    const fetcher = vi.fn().mockResolvedValue(makeResponse([{ id: "r" }]));
    const { result } = renderHook(() =>
      useCursorList<Row, undefined>({ fetcher, filtro: undefined }),
    );

    await waitFor(() => expect(result.current.loading).toBe(false));
    const callsBefore = fetcher.mock.calls.length;

    act(() => result.current.reload());
    await waitFor(() => expect(fetcher.mock.calls.length).toBeGreaterThan(callsBefore));
  });
});
