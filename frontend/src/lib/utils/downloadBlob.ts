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
  const blob = new Blob([res.data as BlobPart], { type: MIME[formato] });
  downloadBlob(blob, `meus-dados.${formato}`);
}
