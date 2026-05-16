import { useState, useEffect, useCallback } from "react";
import type { PaginatedResponse } from "@/types";

interface Options<T> {
  fetcher: (page: number, pageSize: number) => Promise<PaginatedResponse<T>>;
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

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const data = await fetcher(page, pageSize);
      setItems(data.items as T[]);
      setTotal(data.total);
    } catch {
      setError(errorMessage);
    } finally {
      setLoading(false);
    }
  }, [fetcher, page, pageSize, errorMessage]);

  useEffect(() => { load(); }, [load]);

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
    reload: load,
  };
}
