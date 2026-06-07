import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { contaApi } from "@/lib/api/conta";
import { downloadBlob, baixarMeusDados } from "./downloadBlob";

describe("downloadBlob", () => {
  beforeEach(() => {
    vi.spyOn(URL, "createObjectURL").mockReturnValue("blob:fake");
    vi.spyOn(URL, "revokeObjectURL").mockImplementation(() => {});
  });

  afterEach(() => vi.restoreAllMocks());

  it("creates an anchor, triggers download with given filename, and revokes the URL", () => {
    const clickSpy = vi.fn();
    const realCreate = document.createElement.bind(document);
    vi.spyOn(document, "createElement").mockImplementation((tag: string) => {
      const el = realCreate(tag as keyof HTMLElementTagNameMap);
      if (tag === "a") vi.spyOn(el as HTMLAnchorElement, "click").mockImplementation(clickSpy);
      return el;
    });

    const blob = new Blob(["{}"], { type: "application/json" });
    downloadBlob(blob, "x.json");

    expect(URL.createObjectURL).toHaveBeenCalledWith(blob);
    expect(clickSpy).toHaveBeenCalledOnce();
    expect(URL.revokeObjectURL).toHaveBeenCalledWith("blob:fake");
  });

  it("baixarMeusDados fetches exportarDados and downloads meus-dados.json", async () => {
    const blob = new Blob(["{}"], { type: "application/json" });
    const exportSpy = vi
      .spyOn(contaApi, "exportarDados")
      .mockResolvedValue({ data: blob } as Awaited<ReturnType<typeof contaApi.exportarDados>>);
    const realCreate = document.createElement.bind(document);
    let downloadName = "";
    vi.spyOn(document, "createElement").mockImplementation((tag: string) => {
      const el = realCreate(tag as keyof HTMLElementTagNameMap);
      if (tag === "a") {
        vi.spyOn(el as HTMLAnchorElement, "click").mockImplementation(() => {
          downloadName = (el as HTMLAnchorElement).download;
        });
      }
      return el;
    });

    await baixarMeusDados();

    expect(exportSpy).toHaveBeenCalledOnce();
    expect(downloadName).toBe("meus-dados.json");
  });
});
