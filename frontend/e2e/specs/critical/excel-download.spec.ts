import { test, expect, useAuthRole } from "../../fixtures/test-base";

/**
 * Critical 8/8 — excel download dispara baixa de arquivo .xlsx valido.
 *
 * Frontend gera .xlsx via exceljs (src/lib/utils/excel.ts). Validamos:
 * - download evento captura ArrayBuffer
 * - magic bytes XLSX (PK header zip)
 * - suggested filename termina em .xlsx
 *
 * Botoes de export existem em paginas admin (historico, pagamentos etc).
 * Spec navega pra /admin/alunos onde tem botao "Exportar" — se nao tiver,
 * skip-a com aviso.
 */

useAuthRole(test, "admin");

test.describe("excel download", () => {
  test("download de planilha gera .xlsx valido", async ({ page }) => {
    await page.goto("/admin/alunos");

    const exportButton = page.getByRole("button", { name: /export/i }).first();
    test.skip(
      (await exportButton.count()) === 0,
      "/admin/alunos sem botao Export — outra pagina precisa cobrir este caso",
    );

    const downloadPromise = page.waitForEvent("download");
    await exportButton.click();
    const download = await downloadPromise;

    expect(download.suggestedFilename()).toMatch(/\.xlsx$/);

    const stream = await download.createReadStream();
    expect(stream, "download deve ter stream").toBeTruthy();
    const chunks: Buffer[] = [];
    for await (const chunk of stream!) chunks.push(chunk as Buffer);
    const buffer = Buffer.concat(chunks);

    // XLSX = ZIP magic: 'PK' (50 4B 03 04)
    expect(buffer.length).toBeGreaterThan(4);
    expect(buffer[0]).toBe(0x50);
    expect(buffer[1]).toBe(0x4b);
    expect(buffer[2]).toBe(0x03);
    expect(buffer[3]).toBe(0x04);
  });
});
