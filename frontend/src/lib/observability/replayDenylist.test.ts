import { describe, it, expect } from "vitest";
import { isReplayDenied } from "./replayDenylist";

describe("isReplayDenied", () => {
  it.each(["/admin/saude", "/admin/saude/", "/cadastro/aluno?x=1"])(
    "nega replay em rota sensível: %s",
    (path) => {
      expect(isReplayDenied(path)).toBe(true);
    },
  );

  it.each(["/admin/saudemax", "/admin", "/cadastro/treinador"])(
    "permite replay em rota não-listada: %s",
    (path) => {
      expect(isReplayDenied(path)).toBe(false);
    },
  );
});
