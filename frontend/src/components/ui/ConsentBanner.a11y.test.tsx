import { render } from "@testing-library/react";
import { axe } from "vitest-axe";
import { describe, it, expect, beforeEach } from "vitest";
import ConsentBanner from "./ConsentBanner";

function clearConsentCookie() {
  document.cookie = "consent=; max-age=0; path=/";
}

describe("ConsentBanner a11y", () => {
  beforeEach(() => {
    clearConsentCookie();
  });

  it("dialog aberto (sem consentimento prévio) sem violações", async () => {
    const { container } = render(<ConsentBanner />);
    expect(await axe(container)).toHaveNoViolations();
  });

  it("dialog forçado aberto (preferências) sem violações", async () => {
    const { container } = render(<ConsentBanner forceOpen />);
    expect(await axe(container)).toHaveNoViolations();
  });
});
