"use client";
import { useQuery } from "@tanstack/react-query";
import { queryKeys } from "@/lib/query/keys";
import { treinadorApi } from "@/lib/api/treinador";

export function useTreinadorDashboard() {
  return useQuery({
    queryKey: queryKeys.treinador.dashboard,
    staleTime: 60 * 1000,
    queryFn: () => treinadorApi.getDashboard().then((r) => r.data),
  });
}
