import { useState, useEffect, useCallback, useRef } from "react";
import type { PaginatedResponse } from "@/types";

interface Options<T> {
  fetcher: (page: number, pageSize: number, signal: AbortSignal) => Promise<PaginatedResponse<T>>;
  errorMessage?: string;
  initialPageSize?: number;
}

export function usePaginatedList<T>({
  fetcher,
  errorMessage = "Erro ao carregar.",
  initialPageSize = 10,
}: Options<T>) {
  const [items, setItems] = useState<T[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(initialPageSize);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const abortRef = useRef<AbortController | null>(null);

  const load = useCallback(async (signal: AbortSignal) => {
    setLoading(true);
    setError("");
    setSuccess("");
    try {
      const data = await fetcher(page, pageSize, signal);
      if (!signal.aborted) {
        setItems(data.items as T[]);
        setTotal(data.total);
      }
    } catch (err) {
      if (!signal.aborted) setError(errorMessage);
    } finally {
      if (!signal.aborted) setLoading(false);
    }
  }, [fetcher, page, pageSize, errorMessage]);

  useEffect(() => {
    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;
    load(controller.signal);
    return () => { controller.abort(); };
  }, [load]);

  const reload = useCallback(() => {
    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;
    load(controller.signal);
  }, [load]);

  const handlePageChange = useCallback((newPage: number) => setPage(newPage), []);
  const handlePageSizeChange = useCallback((newSize: number) => { setPageSize(newSize); setPage(0); }, []);

  return {
    items,
    total,
    page,
    pageSize,
    loading,
    error,
    success,
    setPage: handlePageChange,
    setPageSize: handlePageSizeChange,
    setError,
    setSuccess,
    reload,
  };
}
