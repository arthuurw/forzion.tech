import { render } from "@testing-library/react";
import { axe } from "vitest-axe";
import { describe, it, expect } from "vitest";
import LoadingSpinner from "./LoadingSpinner";

describe("LoadingSpinner a11y", () => {
  it("inline", async () => {
    const { container } = render(<LoadingSpinner />);
    expect(await axe(container)).toHaveNoViolations();
  });

  it("fullPage", async () => {
    const { container } = render(<LoadingSpinner fullPage />);
    expect(await axe(container)).toHaveNoViolations();
  });
});
