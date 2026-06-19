import { describe, expect, it } from "vitest";
import { parseYouTubeId, youtubeEmbedUrl, youtubeThumb } from "@/lib/utils/youtube";

describe("parseYouTubeId", () => {
  it.each([
    ["dQw4w9WgXcQ", "dQw4w9WgXcQ"],
    ["https://www.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ"],
    ["https://youtu.be/dQw4w9WgXcQ", "dQw4w9WgXcQ"],
    ["https://www.youtube.com/shorts/dQw4w9WgXcQ", "dQw4w9WgXcQ"],
    ["https://www.youtube.com/embed/dQw4w9WgXcQ", "dQw4w9WgXcQ"],
    ["https://youtu.be/dQw4w9WgXcQ?t=42", "dQw4w9WgXcQ"],
    ["https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=PL123", "dQw4w9WgXcQ"],
    ["  https://www.youtube.com/watch?v=dQw4w9WgXcQ  ", "dQw4w9WgXcQ"],
    ["a_B-c1D2e3F", "a_B-c1D2e3F"],
  ])("extrai id de %s", (entrada, esperado) => {
    expect(parseYouTubeId(entrada)).toBe(esperado);
  });

  it.each([
    [""],
    ["   "],
    [null],
    [undefined],
    ["dQw4w9WgXc"],
    ["dQw4w9WgXcQX"],
    ["https://vimeo.com/123456789"],
    ["não é url"],
    ["https://www.youtube.com/watch?v=short"],
  ])("retorna null para %s", (entrada) => {
    expect(parseYouTubeId(entrada)).toBeNull();
  });
});

describe("youtubeThumb", () => {
  it("monta thumb em i.ytimg.com", () => {
    expect(youtubeThumb("dQw4w9WgXcQ")).toBe("https://i.ytimg.com/vi/dQw4w9WgXcQ/hqdefault.jpg");
  });

  it("retorna null para id inválido", () => {
    expect(youtubeThumb("lixo")).toBeNull();
  });
});

describe("youtubeEmbedUrl", () => {
  it("monta embed nocookie com rel=0", () => {
    expect(youtubeEmbedUrl("https://youtu.be/dQw4w9WgXcQ")).toBe(
      "https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ?rel=0",
    );
  });

  it("retorna null para id inválido", () => {
    expect(youtubeEmbedUrl("javascript:alert(1)")).toBeNull();
  });
});
