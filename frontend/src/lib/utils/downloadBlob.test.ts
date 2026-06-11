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

  function setupDownloadCapture() {
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
    return { getDownloadName: () => downloadName };
  }

  it("baixarMeusDados('xlsx') calls exportarDados with xlsx and saves meus-dados.xlsx", async () => {
    const blob = new Blob(["binary"], { type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" });
    const exportSpy = vi
      .spyOn(contaApi, "exportarDados")
      .mockResolvedValue({ data: blob } as Awaited<ReturnType<typeof contaApi.exportarDados>>);
    const { getDownloadName } = setupDownloadCapture();

    await baixarMeusDados("xlsx");

    expect(exportSpy).toHaveBeenCalledWith("xlsx");
    expect(getDownloadName()).toBe("meus-dados.xlsx");
  });

  it("baixarMeusDados('json') calls exportarDados with json and saves meus-dados.json", async () => {
    const blob = new Blob(["{}"], { type: "application/json" });
    const exportSpy = vi
      .spyOn(contaApi, "exportarDados")
      .mockResolvedValue({ data: blob } as Awaited<ReturnType<typeof contaApi.exportarDados>>);
    const { getDownloadName } = setupDownloadCapture();

    await baixarMeusDados("json");

    expect(exportSpy).toHaveBeenCalledWith("json");
    expect(getDownloadName()).toBe("meus-dados.json");
  });

  it("baixarMeusDados default formato is xlsx", async () => {
    const blob = new Blob(["binary"]);
    const exportSpy = vi
      .spyOn(contaApi, "exportarDados")
      .mockResolvedValue({ data: blob } as Awaited<ReturnType<typeof contaApi.exportarDados>>);
    setupDownloadCapture();

    await baixarMeusDados();

    expect(exportSpy).toHaveBeenCalledWith("xlsx");
  });
});
