import { describe, it, expect } from "vitest";
import { screen } from "@testing-library/react";
import { renderLanding, setupLandingTest } from "@/test/helpers/landing";

setupLandingTest();

describe("LandingPage — a11y headings (FPAD-04)", () => {
  it("tem um único h1 e ele é o título do hero", async () => {
    await renderLanding([]);
    const h1s = screen.getAllByRole("heading", { level: 1 });
    expect(h1s).toHaveLength(1);
    expect(h1s[0]).toHaveTextContent(/Profissionalize a gestão/);
  }, 20000);
});
