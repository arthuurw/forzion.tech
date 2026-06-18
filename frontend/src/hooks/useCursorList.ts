import { useState, useEffect, useCallback, useRef } from "react";

interface CursorListResponse<T> {
  itens: T[];
  proximoCursor?: string | null;
}

interface Options<T, F> {
  fetcher: (filtro: F, aposId: string | undefined, signal: AbortSignal) => Promise<{ data: CursorListResponse<T> }>;
  filtro: F;
  errorMessage?: string;
}

interface CursorListState<T> {
  itens: T[];
  cursor: string | null;
  loading: boolean;
  loadingMais: boolean;
  error: string;
  setError: (v: string) => void;
  carregarMais: () => Promise<void>;
  reload: () => void;
}

export function useCursorList<T, F = undefined>({
  fetcher,
  filtro,
  errorMessage = "Erro ao carregar.",
}: Options<T, F>): CursorListState<T> {
  const [itens, setItens] = useState<T[]>([]);
  const [cursor, setCursor] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadingMais, setLoadingMais] = useState(false);
  const [error, setError] = useState("");
  const abortRef = useRef<AbortController | null>(null);

  const fetchPage = useCallback(
    async (aposId: string | undefined, signal: AbortSignal) => {
      const res = await fetcher(filtro, aposId, signal);
      if (signal.aborted) return;
      setItens((prev) => (aposId ? [...prev, ...res.data.itens] : res.data.itens));
      setCursor(res.data.proximoCursor ?? null);
    },
    [fetcher, filtro],
  );

  const initialLoad = useCallback(() => {
    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;
    const { signal } = controller;
    setLoading(true);
    setError("");
    fetchPage(undefined, signal)
      .catch(() => { if (!signal.aborted) setError(errorMessage); })
      .finally(() => { if (!signal.aborted) setLoading(false); });
  }, [fetchPage, errorMessage]);

  useEffect(() => {
    initialLoad();
    return () => { abortRef.current?.abort(); };
  }, [initialLoad]);

  const carregarMais = useCallback(async () => {
    if (!cursor) return;
    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;
    const { signal } = controller;
    setLoadingMais(true);
    try {
      await fetchPage(cursor, signal);
    } catch {
      if (!signal.aborted) setError(errorMessage);
    } finally {
      if (!signal.aborted) setLoadingMais(false);
    }
  }, [cursor, fetchPage, errorMessage]);

  const reload = useCallback(() => {
    initialLoad();
  }, [initialLoad]);

  return { itens, cursor, loading, loadingMais, error, setError, carregarMais, reload };
}
