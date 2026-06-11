import { contaApi } from "@/lib/api/conta";

const MIME: Record<"xlsx" | "json", string> = {
  xlsx: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
  json: "application/json",
};

export function downloadBlob(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}

export async function baixarMeusDados(formato: "xlsx" | "json" = "xlsx"): Promise<void> {
  const res = await contaApi.exportarDados(formato);
  const raw = res.data as Blob;
  // slice(0, size, type) é a forma idiomática de sobrescrever o MIME de um Blob
  // existente sem recriar o buffer — garante tipo correto independente dos headers.
  const blob = raw.slice(0, raw.size, MIME[formato]);
  downloadBlob(blob, `meus-dados.${formato}`);
}
