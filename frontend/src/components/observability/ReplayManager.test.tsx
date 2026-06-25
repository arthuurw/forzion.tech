import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, waitFor } from "@testing-library/react";

vi.hoisted(() => {
  process.env.NEXT_PUBLIC_SENTRY_DSN = "https://public@example.ingest.sentry.io/1";
});

const addIntegration = vi.fn();
const replayIntegration = vi.fn((opts: unknown) => ({ __replay: opts }));
const start = vi.fn();
const stop = vi.fn(() => Promise.resolve());
const getReplay = vi.fn(() => ({ start, stop }));

vi.mock("@sentry/nextjs", () => ({ addIntegration, replayIntegration, getReplay }));

let mockPathname = "/aluno/fichas";
vi.mock("next/navigation", () => ({ usePathname: () => mockPathname }));

let mockConsent: { v: 1; analytics: boolean } | null = { v: 1, analytics: true };
vi.mock("@/hooks/useConsent", () => ({ useConsent: () => ({ consent: mockConsent }) }));

import { ReplayManager } from "./ReplayManager";

beforeEach(() => {
  vi.clearAllMocks();
  mockPathname = "/aluno/fichas";
  mockConsent = { v: 1, analytics: true };
  // força o fallback setTimeout do onIdle (determinístico sob jsdom)
  (window as unknown as { requestIdleCallback?: unknown }).requestIdleCallback = undefined;
});

describe("ReplayManager", () => {
  it("(a) rota permitida + consent → adiciona replayIntegration", async () => {
    render(<ReplayManager />);
    await waitFor(() => expect(addIntegration).toHaveBeenCalledTimes(1));
    expect(replayIntegration).toHaveBeenCalledWith({ maskAllText: true, blockAllMedia: true });
  });

  it("(b) rota excluída → não adiciona replay", async () => {
    mockPathname = "/admin/saude";
    render(<ReplayManager />);
    await Promise.resolve();
    await new Promise((r) => setTimeout(r, 5));
    expect(addIntegration).not.toHaveBeenCalled();
  });

  it("(c) navegação permitida→excluída → stop()", async () => {
    const { rerender } = render(<ReplayManager />);
    await waitFor(() => expect(addIntegration).toHaveBeenCalledTimes(1));

    mockPathname = "/cadastro/aluno";
    rerender(<ReplayManager />);
    await waitFor(() => expect(stop).toHaveBeenCalledTimes(1));
  });

  it("(d) navegação excluída→permitida → adiciona replay", async () => {
    mockPathname = "/cadastro/aluno";
    const { rerender } = render(<ReplayManager />);
    await new Promise((r) => setTimeout(r, 5));
    expect(addIntegration).not.toHaveBeenCalled();

    mockPathname = "/aluno/fichas";
    rerender(<ReplayManager />);
    await waitFor(() => expect(addIntegration).toHaveBeenCalledTimes(1));
  });

  it("(e) sem consent → não dispara dynamic import (nada gravado)", async () => {
    mockConsent = { v: 1, analytics: false };
    render(<ReplayManager />);
    await new Promise((r) => setTimeout(r, 5));
    expect(addIntegration).not.toHaveBeenCalled();
    expect(getReplay).not.toHaveBeenCalled();
  });
});
