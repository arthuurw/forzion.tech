import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import ConsentBanner from "../ConsentBanner";

function setCookieConsent(value: string | null) {
  if (value === null) {
    document.cookie = "consent=; max-age=0; path=/";
  } else {
    document.cookie = `consent=${encodeURIComponent(value)}; path=/`;
  }
}

function getConsentCookie(): string | null {
  const match = document.cookie
    .split("; ")
    .find((row) => row.startsWith("consent="));
  if (!match) return null;
  return decodeURIComponent(match.split("=").slice(1).join("="));
}

describe("ConsentBanner", () => {
  beforeEach(() => {
    setCookieConsent(null);
  });

  it("renders banner when no consent cookie exists", () => {
    render(<ConsentBanner />);
    expect(
      screen.getByRole("dialog", { name: /cookie|consentimento|lgpd/i }),
    ).toBeInTheDocument();
  });

  it("renders main buttons: Aceitar todos, Só essenciais, Preferências", () => {
    render(<ConsentBanner />);
    expect(screen.getByRole("button", { name: /aceitar todos/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /só essenciais/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /preferências/i })).toBeInTheDocument();
  });

  it("discloses third parties and links to the privacy policy", () => {
    render(<ConsentBanner />);
    expect(screen.getByText(/terceiros \(Resend, Stripe, Meta\)/i)).toBeInTheDocument();
    const link = screen.getByRole("link", { name: /política de privacidade/i });
    expect(link).toHaveAttribute("href", "/privacidade");
  });

  it("Aceitar todos persists analytics=true in cookie", async () => {
    render(<ConsentBanner />);
    fireEvent.click(screen.getByRole("button", { name: /aceitar todos/i }));

    await waitFor(() => {
      const raw = getConsentCookie();
      expect(raw).not.toBeNull();
      const parsed = JSON.parse(raw!);
      expect(parsed.v).toBe(1);
      expect(parsed.analytics).toBe(true);
    });
  });

  it("Só essenciais persists analytics=false in cookie", async () => {
    render(<ConsentBanner />);
    fireEvent.click(screen.getByRole("button", { name: /só essenciais/i }));

    await waitFor(() => {
      const raw = getConsentCookie();
      expect(raw).not.toBeNull();
      const parsed = JSON.parse(raw!);
      expect(parsed.v).toBe(1);
      expect(parsed.analytics).toBe(false);
    });
  });

  it("Preferências flow: toggle analytics and save", async () => {
    render(<ConsentBanner />);
    fireEvent.click(screen.getByRole("button", { name: /preferências/i }));

    expect(screen.getByText(/análise/i)).toBeInTheDocument();

    // MUI Switch renders role="switch", not "checkbox"
    const switches = screen.getAllByRole("switch", { hidden: true });
    // First switch is "Essenciais" (disabled), second is "Análise"
    const analyticsSwitch = switches[1];
    expect(analyticsSwitch).not.toBeChecked();
    fireEvent.click(analyticsSwitch);

    fireEvent.click(screen.getByRole("button", { name: /salvar preferências/i }));

    await waitFor(() => {
      const raw = getConsentCookie();
      expect(raw).not.toBeNull();
      const parsed = JSON.parse(raw!);
      expect(parsed.analytics).toBe(true);
    });
  });

  it("does not render banner when consent cookie already set", () => {
    setCookieConsent(JSON.stringify({ v: 1, analytics: false }));
    render(<ConsentBanner />);
    expect(
      screen.queryByRole("dialog"),
    ).not.toBeInTheDocument();
  });

  it("forceOpen=true shows banner even when consent already set", () => {
    setCookieConsent(JSON.stringify({ v: 1, analytics: false }));
    const onClose = vi.fn();
    render(<ConsentBanner forceOpen onClose={onClose} />);
    expect(
      screen.getByRole("dialog", { name: /cookie|consentimento|lgpd/i }),
    ).toBeInTheDocument();
  });
});

describe("Sentry consent gate (instrumentation-client behavior)", () => {
  it("analytics consent defaults to OFF (false) when no cookie", () => {
    // Simulate what instrumentation-client.ts does: read cookie, check analytics
    setCookieConsent(null);
    const raw = document.cookie
      .split("; ")
      .find((row) => row.startsWith("consent="));
    expect(raw).toBeUndefined();

    let parsed: { v: number; analytics: boolean } | null = null;
    if (raw) {
      try {
        parsed = JSON.parse(decodeURIComponent(raw.split("=").slice(1).join("=")));
      } catch {
        parsed = null;
      }
    }
    const analyticsConsented = parsed?.analytics === true;
    expect(analyticsConsented).toBe(false);
  });

  it("analytics consent is false when user chose essencial-only", () => {
    setCookieConsent(JSON.stringify({ v: 1, analytics: false }));
    const raw = document.cookie
      .split("; ")
      .find((row) => row.startsWith("consent="));
    const parsed = JSON.parse(decodeURIComponent(raw!.split("=").slice(1).join("=")));
    expect(parsed.analytics).toBe(false);
  });

  it("analytics consent is true when user accepted all", () => {
    setCookieConsent(JSON.stringify({ v: 1, analytics: true }));
    const raw = document.cookie
      .split("; ")
      .find((row) => row.startsWith("consent="));
    const parsed = JSON.parse(decodeURIComponent(raw!.split("=").slice(1).join("=")));
    expect(parsed.analytics).toBe(true);
  });
});
