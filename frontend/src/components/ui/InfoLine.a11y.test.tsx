import { render } from "@testing-library/react";
import { axe } from "vitest-axe";
import { describe, it, expect } from "vitest";
import InfoLine from "./InfoLine";

describe("InfoLine a11y", () => {
  it("sem violações", async () => {
    const { container } = render(<InfoLine label="E-mail" value="maria@forzion.tech" />);
    expect(await axe(container)).toHaveNoViolations();
  });
});
